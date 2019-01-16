using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    public class ScopeBuilder
    {
        private protected readonly IDictionary<string, ParameterExpression> variables;
        private readonly ICollection<Expression> statements;

        internal ScopeBuilder(ScopeBuilder parent = null)
        {
            Parent = parent;
            variables = new Dictionary<string, ParameterExpression>();
            statements = new LinkedList<Expression>();
        }

        /// <summary>
        /// Sets body of lambda expression as single expression.
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
        public ScopeBuilder Parent{ get; }

        internal E AddStatement<E>(E expression)
            where E: Expression
        {
            statements.Add(expression);
            return expression;
        }

        public BinaryExpression Assign(ParameterExpression variable, Expression value)
            => AddStatement(Expression.Assign(variable, value));

        public void Assign(string variableName, Expression value)
            => Assign(this[variableName], value);
        
        public void Assign(Expression instance, PropertyInfo instanceProperty, Expression value)
            => AddStatement(Expression.Assign(Expression.Property(instance, instanceProperty), value));
        
        public void Assign(PropertyInfo staticProperty, Expression value)
            => Assign(null, staticProperty, value);
        
        public void Assign(Expression instance, FieldInfo instanceField, Expression value)
            => AddStatement(Expression.Assign(Expression.Field(instance, instanceField), value));

        public void AssignStatement(FieldInfo instanceField, Expression value)
            => Assign(null, instanceField, value);
        
        public MethodCallExpression Call(Expression instance, MethodInfo method, IEnumerable<Expression> arguments)
            => AddStatement(Expression.Call(instance, method, arguments));
        
        public MethodCallExpression Call(Expression instance, MethodInfo method, params Expression[] arguments)
            => Call(instance, method, arguments.Upcast<IEnumerable<Expression>, Expression[]>());

        public MethodCallExpression Call(MethodInfo method, IEnumerable<Expression> arguments)
            => Call(null, method, arguments);
        
        public MethodCallExpression Call(MethodInfo method, params Expression[] arguments)
            => Call(null, method, arguments);
        
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

        public GotoExpression Goto(LabelTarget target, Expression value)
            => AddStatement(Expression.Goto(target, value));
        
        public GotoExpression Goto(LabelTarget target) => Goto(target, null);

        private bool HasVariable(string name) => variables.ContainsKey(name) || Parent != null && Parent.HasVariable(name);
        
        public ParameterExpression this[string localVariableName]
            => variables.TryGetValue(localVariableName, out var variable) ? variable : Parent?[localVariableName];

        private protected void DeclareVariable(ParameterExpression variable)
            => variables.Add(variable.Name, variable);

        public ParameterExpression DeclareVariable<T>(string name, bool byRef = false)
            => DeclareVariable(byRef ? typeof(T).MakeByRefType() : typeof(T), name);

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

        public ConditionalBuilder If(Expression test)
            => new ConditionalBuilder(test, this, true);

        public ConditionalExpression IfThen(Expression test, Action<ScopeBuilder> ifTrue)
            => If(test).Then(ifTrue).EndIf();

        public ConditionalExpression IfThenElse(Expression test, Action<ScopeBuilder> ifTrue, Action<ScopeBuilder> ifFalse)
            => If(test).Then(ifTrue).Else(ifFalse).EndIf();
        
        public ConditionalBuilder Condition(Expression test)
            => new ConditionalBuilder(test, this, false);

        public ConditionalExpression Condition(Expression test, Type type, Action<ScopeBuilder> ifTrue, Action<ScopeBuilder> ifFalse)
            => Condition(test).Then(ifTrue).Else(ifFalse).EndIf(type);

        private LoopExpression WhileLoop(Expression test, Action<WhileLoopBuider> loop, bool conditionFirst)
        {
            var builder = new WhileLoopBuider(test, this, conditionFirst);
            loop(builder);
            var expr =  builder.BuildExpression();
            AddStatement(expr);
            return expr;
        }

        public LoopExpression While(Expression test, Action<WhileLoopBuider> loop)
            => WhileLoop(test, loop, true);

        public LoopExpression DoWhile(Expression test, Action<WhileLoopBuider> loop)
            => WhileLoop(test, loop, false);

        public Expression ForEach(Expression collection, Action<ForEachLoopBuilder> loop)
        {
            var builder = new ForEachLoopBuilder(collection, this);
            loop(builder);
            var expr = builder.BuildExpression();
            AddStatement(expr);
            return expr;
        }

        public LoopExpression Loop(Action<LoopBuilder> loop)
        {
            var builder = new LoopBuilder(this);
            loop(builder);
            var expr = builder.BuildExpression();
            AddStatement(expr);
            return expr;
        }

        public GotoExpression Continue(LoopBuilder loop)
            => AddStatement(loop.Continue());

        public GotoExpression Break(LoopBuilder loop)
            => AddStatement(loop.Break());

        internal Expression BuildExpression()
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
    }
}
