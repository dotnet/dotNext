using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using static Threading.AtomicLong;

    /// <summary>
    /// Represents lexical scope and methods for adding expressions
    /// and statements to it.
    /// </summary>
    public class ExpressionBuilder: Disposable
    {
        private readonly IDictionary<string, ParameterExpression> variables;
        private readonly ICollection<Expression> statements;
        private long nameGenerator;

        internal ExpressionBuilder(ExpressionBuilder parent = null)
        {
            Parent = parent;
            variables = new Dictionary<string, ParameterExpression>();
            statements = new LinkedList<Expression>();
        }

        internal string NextName(string prefix) => Parent is null ? prefix + nameGenerator.IncrementAndGet() : Parent.NextName(prefix);

        /// <summary>
        /// Sets body of this scope as single expression.
        /// </summary>
        public virtual Expression Body
        {
            set
            {
                variables.Clear();
                statements.Clear();
                statements.Add(value);
            }
        }

        /// <summary>
        /// Represents parent scope.
        /// </summary>
        public ExpressionBuilder Parent{ get; }

        public E AddStatement<E>(E expression)
            where E: Expression
        {
            statements.Add(expression);
            return expression;
        }

        internal static E Build<E, B>(B builder, Action<B> body)
            where E: Expression
            where B: IExpressionBuilder<E>
        {
            body(builder);
            return builder.Build();
        }

        internal Expression Build(Action<ExpressionBuilder> body)
        {
            body(this);
            return Build();
        }

        private E AddStatement<E, B>(B builder, Action<B> body)
            where E: Expression
            where B: IExpressionBuilder<E>
            => AddStatement(Build<E, B>(builder, body));

        public ConstantExpression Constant<T>(T value) => AddStatement(Expression.Constant(value, typeof(T)));

        public BinaryExpression Assign(ParameterExpression variable, UniversalExpression value)
            => AddStatement(Expression.Assign(variable, value));

        public void Assign(string variableName, UniversalExpression value)
            => Assign(this[variableName], value);
        
        public void Assign(Expression instance, PropertyInfo instanceProperty, UniversalExpression value)
            => AddStatement(Expression.Assign(Expression.Property(instance, instanceProperty), value));
        
        public void Assign(PropertyInfo staticProperty, UniversalExpression value)
            => Assign(null, staticProperty, value);
        
        public void Assign(Expression instance, FieldInfo instanceField, UniversalExpression value)
            => AddStatement(Expression.Assign(Expression.Field(instance, instanceField), value));

        public void Assign(FieldInfo instanceField, UniversalExpression value)
            => Assign(null, instanceField, value);

        public InvocationExpression Invoke(UniversalExpression @delegate, IEnumerable<Expression> arguments)
            => AddStatement(Expression.Invoke(@delegate, arguments));

        public InvocationExpression Invoke(UniversalExpression @delegate, params UniversalExpression[] arguments)
            => AddStatement(@delegate.Invoke(arguments));
        
        public MethodCallExpression Call(UniversalExpression instance, MethodInfo method, IEnumerable<Expression> arguments)
            => AddStatement(Expression.Call(instance, method, arguments));

        public MethodCallExpression Call(UniversalExpression instance, MethodInfo method, params UniversalExpression[] arguments)
            => Call(instance, method, UniversalExpression.AsExpressions(arguments.Upcast<IEnumerable<UniversalExpression>, UniversalExpression[]>()));

        public MethodCallExpression Call(MethodInfo method, IEnumerable<Expression> arguments)
            => AddStatement(Expression.Call(null, method, arguments));

        public MethodCallExpression Call(MethodInfo method, params UniversalExpression[] arguments)
            => Call(method, UniversalExpression.AsExpressions(arguments.Upcast<IEnumerable<UniversalExpression>, UniversalExpression[]>()));
        
        public LabelTarget Label(Type type, string name = null)
        {
            var target = Expression.Label(type, name);
            Label(target);
            return target;
        }

        public LabelTarget Label<T>(string name = null) => Label(typeof(T), name);

        public LabelTarget Label() => Label(typeof(void));

        public LabelExpression Label(LabelTarget target)
            => AddStatement(Expression.Label(target));

        public GotoExpression Goto(LabelTarget target, UniversalExpression value)
            => AddStatement(Expression.Goto(target, value));

        public GotoExpression Goto(LabelTarget target) => Goto(target, default);

        private bool HasVariable(string name) => variables.ContainsKey(name) || Parent != null && Parent.HasVariable(name);
        
        public ParameterExpression this[string localVariableName]
            => variables.TryGetValue(localVariableName, out var variable) ? variable : Parent?[localVariableName];

        private protected void DeclareVariable(ParameterExpression variable)
            => variables.Add(variable.Name, variable);

        public ParameterExpression DeclareVariable<T>(string name)
            => DeclareVariable(typeof(T), name);

        public ParameterExpression DeclareVariable<T>(string name, T initialValue)
        {
            var variable = DeclareVariable<T>(name);
            Assign(variable, Expression.Constant(initialValue, typeof(T)));
            return variable;
        }

        public ParameterExpression DeclareVariable(Type variableType, string name, bool initialize = false)
        {
            var variable = Expression.Variable(variableType, name);
            variables.Add(name, variable);
            if (initialize)
                Assign(variable, Expression.Default(variableType));
            return variable;
        }

        public ConditionalBuilder If(UniversalExpression test)
            => new ConditionalBuilder(test, this, true);

        public ConditionalExpression IfThen(UniversalExpression test, Action<ExpressionBuilder> ifTrue)
            => If(test).Then(ifTrue).End();

        public ConditionalExpression IfThenElse(UniversalExpression test, Action<ExpressionBuilder> ifTrue, Action<ExpressionBuilder> ifFalse)
            => If(test).Then(ifTrue).Else(ifFalse).End();

        public LoopExpression While(UniversalExpression test, Action<WhileLoopBuider> loop)
            => AddStatement<LoopExpression, WhileLoopBuider>(new WhileLoopBuider(test, this, true), loop);

        public LoopExpression DoWhile(UniversalExpression test, Action<WhileLoopBuider> loop)
            => AddStatement<LoopExpression, WhileLoopBuider>(new WhileLoopBuider(test, this, false), loop);

        public Expression ForEach(UniversalExpression collection, Action<ForEachLoopBuilder> loop)
            => AddStatement<Expression, ForEachLoopBuilder>(new ForEachLoopBuilder(collection, this), loop);

        public LoopExpression For(UniversalExpression initializer, Func<UniversalExpression, Expression> condition, Action<ForLoopBuilder> loop)
            => AddStatement<LoopExpression, ForLoopBuilder>(new ForLoopBuilder(initializer, condition, this), loop);

        public LoopExpression Loop(Action<LoopBuilder> loop)
            => AddStatement<LoopExpression, LoopBuilder>(new LoopBuilder(this), loop);

        public GotoExpression Continue(LoopBuilderBase loop)
            => AddStatement(loop.Continue(false));

        /// <summary>
        /// Stops the specified loop.
        /// </summary>
        /// <param name="loop">Loop identifier.</param>
        /// <returns>An expression representing jumping outside of the loop.</returns>
        public GotoExpression Break(LoopBuilderBase loop)
            => AddStatement(loop.Break(false));
        
        public Expression Return(LambdaBuilder lambda) => AddStatement(lambda.Return(false));

        public Expression Return(LambdaBuilder lambda, UniversalExpression result) => AddStatement(lambda.Return(result, false));

        public LambdaExpression Lambda<D>(Action<LambdaBuilder> lambda)
            where D: Delegate
            => AddStatement<LambdaExpression, LambdaBuilder<D>>(new LambdaBuilder<D>(this), lambda);

        public TryBuilder Try(UniversalExpression body) => new TryBuilder(body, this, true);

        public Expression Scope(Action<ExpressionBuilder> scope)
            => new ExpressionBuilder(this).Build(scope);

        public Expression With(UniversalExpression expression, Action<WithBlockBuilder> scope)
            => AddStatement<Expression, WithBlockBuilder>(new WithBlockBuilder(expression, this), scope);

        public TryExpression Using(UniversalExpression expression, Action<UsingBlockBuilder> scope)
            => AddStatement<TryExpression, UsingBlockBuilder>(new UsingBlockBuilder(expression, this), scope);

        public SwitchBuilder Switch(UniversalExpression switchValue)
            => new SwitchBuilder(switchValue, this, true);

        internal virtual Expression Build()
        {
            switch(statements.Count)
            {
                case 0:
                    return Expression.Empty();
                case 1:
                    if(variables.Count == 0 && statements.Count == 1)
                        return statements.First();
                    else
                        goto default;
                default:
                    return Expression.Block(variables.Values, statements);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                variables.Clear();
                statements.Clear();
            }
        }
    }

    public abstract class ExpressionBuilder<E> : IExpressionBuilder<E>
        where E : Expression
    {
        private readonly ExpressionBuilder parent;
        private readonly bool treatAsStatement;
        private Type expressionType;

        private protected ExpressionBuilder(ExpressionBuilder parent, bool treatAsStatement)
        {
            this.parent = parent;
            this.treatAsStatement = treatAsStatement;
        }

        private protected ExpressionBuilder NewScope() => new ExpressionBuilder(parent);

        private protected B NewScope<B>(Func<ExpressionBuilder, B> factory) => factory(parent);

        private protected Type ExpressionType
        {
            get => expressionType ?? typeof(void);
        }

        public ExpressionBuilder<E> OfType(Type expressionType)
        {
            this.expressionType = expressionType;
            return this;
        }

        public ExpressionBuilder<E> OfType<T>() => OfType(typeof(T));

        public E End()
        {
            var expr = Build();
            if (treatAsStatement)
                parent.AddStatement(expr);
            return expr;
        }

        private protected abstract E Build();

        E IExpressionBuilder<E>.Build() => Build();
    }
}
