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

        public BinaryExpression AssignStatement(ParameterExpression variable, Expression value)
            => AddStatement(Expression.Assign(variable, value));

        public void AssignStatement(string variableName, Expression value)
            => AssignStatement(this[variableName], value);
        
        public void AssignStatement(Expression instance, PropertyInfo instanceProperty, Expression value)
            => AddStatement(Expression.Assign(Expression.Property(instance, instanceProperty), value));
        
        public void AssignStatement(PropertyInfo staticProperty, Expression value)
            => AssignStatement(null, staticProperty, value);
        
        public void AssignStatement(Expression instance, FieldInfo instanceField, Expression value)
            => AddStatement(Expression.Assign(Expression.Field(instance, instanceField), value));

        public void AssignStatement(FieldInfo instanceField, Expression value)
            => AssignStatement(null, instanceField, value);
        
        public MethodCallExpression CallStatement(Expression instance, MethodInfo method, IEnumerable<Expression> arguments)
            => AddStatement(Expression.Call(instance, method, arguments));
        
        public MethodCallExpression CallStatement(Expression instance, MethodInfo method, params Expression[] arguments)
            => CallStatement(instance, method, arguments.Upcast<IEnumerable<Expression>, Expression[]>());

        public MethodCallExpression CallStatement(MethodInfo method, IEnumerable<Expression> arguments)
            => CallStatement(null, method, arguments);
        
        public MethodCallExpression CallStatement(MethodInfo method, params Expression[] arguments)
            => CallStatement(null, method, arguments);
        
        public LabelTarget Label(Type type, string name = "")
        {
            var target = Expression.Label(type, name);
            LabelStatement(target);
            return target;
        }

        public LabelExpression LabelStatement(LabelTarget target)
            => AddStatement(Expression.Label(target));

        public GotoExpression GotoStatement(LabelTarget target, Expression value)
            => AddStatement(Expression.Goto(target, value));
        
        public GotoExpression GotoStatement(LabelTarget target) => GotoStatement(target, null);

        private bool HasVariable(string name) => variables.ContainsKey(name) || Parent != null && Parent.HasVariable(name);
        
        public ParameterExpression this[string localVariableName]
            => variables.TryGetValue(localVariableName, out var variable) ? variable : Parent?[localVariableName];

        public ParameterExpression DeclareVariable<T>(string name, bool byRef = false)
            => DeclareVariable(name, byRef ? typeof(T).MakeByRefType() : typeof(T));

        public ParameterExpression DeclareVariable(string name, Type variableType)
        {
            var variable = Expression.Variable(variableType, name);
            variables.Add(name, variable);
            return variable;
        }

        public ConditionalBuilder IfStatement(Expression test)
            => new ConditionalBuilder(test, this, true);
        
        public ConditionalBuilder If(Expression test)
            => new ConditionalBuilder(test, this, false);

        internal Expression BuildExpression()
        {
            switch(statements.Count)
            {
                case 0:
                    return null;
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
