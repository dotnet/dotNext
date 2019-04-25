using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents code generator.
    /// </summary>
    public static class CodeGenerator
    {
        [ThreadStatic]
        private static LexicalScope current;

        private interface ILexicalScopeFactory<out S>
            where S : LexicalScope
        {
            S CreateScope(LexicalScope parent);
        }

        private static S PushScope<F, S>(F factory)
            where S : LexicalScope
            where F : struct, ILexicalScopeFactory<S>
        {
            var scope = factory.CreateScope(current);
            current = scope;
            return scope;
        }

        private static void PopScope() => current = current?.Parent;

        private static S FindScope<S>()
            where S : LexicalScope
        {
            for (var current = CodeGenerator.current; !(current is null); current = current?.Parent)
                if (current is S target)
                    return target;
            return null;
        }

        private static void AddStatement<D, S, F>(F factory, D body)
            where D : Delegate
            where S : LexicalScope, IExpressionBuilder<Expression>, ICompoundStatement<D>
            where F : struct, ILexicalScopeFactory<S>
            => CurrentScope.AddStatement(Build<Expression, D, S, F>(factory, body));

        private static E Build<E, D, S, F>(F factory, D body)
            where D : Delegate
            where E : Expression
            where S : LexicalScope, IExpressionBuilder<E>, ICompoundStatement<D>
            where F : struct, ILexicalScopeFactory<S>
        {
            var scope = PushScope<F, S>(factory);
            E statement;
            try
            {
                scope.ConstructBody(body);
                statement = scope.Build();
            }
            finally
            {
                PopScope();
                scope.Dispose();
            }
            return statement;
        }

        private static B TreatAsStatement<E, B>(this B builder)
            where E : Expression
            where B : ExpressionBuilder<E>
        {
            builder.Constructed += CurrentScope.AddStatement;
            return builder;
        }

        /// <summary>
        /// Gets curremt lexical scope.
        /// </summary>
        private static LexicalScope CurrentScope => current ?? throw new InvalidOperationException(ExceptionMessages.OutOfLexicalScope);

        /// <summary>
        /// Obtains local variable declared in the current or outer lexical scope.
        /// </summary>
        /// <param name="name">The name of the local variable.</param>
        /// <returns>Declared local variable; or <see langword="null"/>, if there is no declared local variable with the given name.</returns>
        public static ParameterExpression Variable(string name)
        {
            for (var current = CodeGenerator.current; !(current is null); current = current?.Parent)
                if (current.Variables.TryGetValue(name, out var variable))
                    return variable;
            return null;
        }

        /// <summary>
        /// Adds no-operation instruction to this scope.
        /// </summary>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Nop() => CurrentScope.AddStatement(Expression.Empty());

        /// <summary>
        /// Adds assignment operation to this scope.
        /// </summary>
        /// <param name="variable">The variable to modify.</param>
        /// <param name="value">The value to be assigned to the variable.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Assign(ParameterExpression variable, Expression value) => CurrentScope.AddStatement(variable.Assign(value));

        /// <summary>
        /// Adds assignment operation to this scope.
        /// </summary>
        /// <param name="indexer">The indexer property or array element to modify.</param>
        /// <param name="value">The value to be assigned to the member or array element.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Assign(IndexExpression indexer, Expression value) => CurrentScope.AddStatement(indexer.Assign(value));

        /// <summary>
        /// Adds assignment operation to this scope.
        /// </summary>
        /// <param name="member">The field or property to modify.</param>
        /// <param name="value">The value to be assigned to the member.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Assign(MemberExpression member, Expression value) => CurrentScope.AddStatement(member.Assign(value));

        /// <summary>
        /// Adds an expression that increments given variable by 1 and assigns the result back to the variable.
        /// </summary>
        /// <param name="variable">The variable to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PreIncrementAssign(ParameterExpression variable) => CurrentScope.AddStatement(variable.PreIncrementAssign());

        /// <summary>
        /// Adds an expression that represents the assignment of given variable followed by a subsequent increment by 1 of the original variable.
        /// </summary>
        /// <param name="variable">The variable to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PostIncrementAssign(ParameterExpression variable) => CurrentScope.AddStatement(variable.PostIncrementAssign());

        /// <summary>
        /// Adds an expression that decrements given variable by 1 and assigns the result back to the variable.
        /// </summary>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PreDecrementAssign(ParameterExpression variable) => CurrentScope.AddStatement(variable.PreDecrementAssign());

        /// <summary>
        /// Adds an expression that represents the assignment of given variable followed by a subsequent decrement by 1 of the original variable.
        /// </summary>
        /// <param name="variable">The variable to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PostDecrementAssign(ParameterExpression variable) => CurrentScope.AddStatement(variable.PostDecrementAssign());

        /// <summary>
        /// Adds an expression that increments given field or property by 1 and assigns the result back to the member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PreIncrementAssign(MemberExpression member) => CurrentScope.AddStatement(member.PreIncrementAssign());

        /// <summary>
        /// Adds an expression that represents the assignment of given field or property followed by a subsequent increment by 1 of the original member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PostIncrementAssign(MemberExpression member) => CurrentScope.AddStatement(member.PostIncrementAssign());

        /// <summary>
        /// Adds an expression that decrements given field or property by 1 and assigns the result back to the member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PreDecrementAssign(MemberExpression member) => CurrentScope.AddStatement(member.PreDecrementAssign());

        /// <summary>
        /// Adds an expression that represents the assignment of given field or property followed by a subsequent decrement by 1 of the original member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PostDecrementAssign(MemberExpression member) => CurrentScope.AddStatement(member.PostDecrementAssign());

        /// <summary>
        /// Adds an expression that increments given field or property by 1 and assigns the result back to the member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PreIncrementAssign(IndexExpression member) => CurrentScope.AddStatement(member.PreIncrementAssign());

        /// <summary>
        /// Adds an expression that represents the assignment of given field or property followed by a subsequent increment by 1 of the original member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PostIncrementAssign(IndexExpression member) => CurrentScope.AddStatement(member.PostIncrementAssign());

        /// <summary>
        /// Adds an expression that decrements given field or property by 1 and assigns the result back to the member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PreDecrementAssign(IndexExpression member) => CurrentScope.AddStatement(member.PreDecrementAssign());

        /// <summary>
        /// Adds an expression that represents the assignment of given field or property followed by a subsequent decrement by 1 of the original member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PostDecrementAssign(IndexExpression member) => CurrentScope.AddStatement(member.PostDecrementAssign());

        /// <summary>
        /// Adds constant as in-place statement.
        /// </summary>
        /// <typeparam name="T">The type of the constant.</typeparam>
        /// <param name="value">The value to be placed as statement.</param>
        public static void InPlaceValue<T>(T value) => CurrentScope.AddStatement(value.Const());

        /// <summary>
        /// Adds local variable assignment operation this scope.
        /// </summary>
        /// <param name="variableName">The name of the declared local variable.</param>
        /// <param name="value">The value to be assigned to the local variable.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Assign(string variableName, Expression value) => Assign(Variable(variableName), value);

        /// <summary>
        /// Adds instance property assignment.
        /// </summary>
        /// <param name="instance"><see langword="this"/> argument.</param>
        /// <param name="instanceProperty">Instance property to be assigned.</param>
        /// <param name="value">A new value of the property.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Assign(Expression instance, PropertyInfo instanceProperty, Expression value)
            => CurrentScope.AddStatement(Expression.Assign(Expression.Property(instance, instanceProperty), value));

        /// <summary>
        /// Adds static property assignment.
        /// </summary>
        /// <param name="staticProperty">Static property to be assigned.</param>
        /// <param name="value">A new value of the property.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Assign(PropertyInfo staticProperty, Expression value)
            => Assign(null, staticProperty, value);

        /// <summary>
        /// Adds instance field assignment.
        /// </summary>
        /// <param name="instance"><see langword="this"/> argument.</param>
        /// <param name="instanceField">Instance field to be assigned.</param>
        /// <param name="value">A new value of the field.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Assign(Expression instance, FieldInfo instanceField, Expression value)
            => CurrentScope.AddStatement(Expression.Assign(Expression.Field(instance, instanceField), value));

        /// <summary>
        /// Adds static field assignment.
        /// </summary>
        /// <param name="staticField">Static field to be assigned.</param>
        /// <param name="value">A new value of the field.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Assign(FieldInfo staticField, Expression value)
            => Assign(null, staticField, value);

        /// <summary>
        /// Adds invocation statement.
        /// </summary>
        /// <param name="delegate">The expression providing delegate to be invoked.</param>
        /// <param name="arguments">Delegate invocation arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Invoke(Expression @delegate, IEnumerable<Expression> arguments) => CurrentScope.AddStatement(Expression.Invoke(@delegate, arguments));

        /// <summary>
        /// Adds invocation statement.
        /// </summary>
        /// <param name="delegate">The expression providing delegate to be invoked.</param>
        /// <param name="arguments">Delegate invocation arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Invoke(Expression @delegate, params Expression[] arguments) => Invoke(@delegate, (IEnumerable<Expression>)arguments);

        /// <summary>
        /// Adds instance method call statement.
        /// </summary>
        /// <param name="instance"><see langword="this"/> argument.</param>
        /// <param name="method">The method to be called.</param>
        /// <param name="arguments">Method call arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Call(Expression instance, MethodInfo method, IEnumerable<Expression> arguments)
            => CurrentScope.AddStatement(Expression.Call(instance, method, arguments));

        /// <summary>
        /// Adds instance method call statement.
        /// </summary>
        /// <param name="instance"><see langword="this"/> argument.</param>
        /// <param name="method">The method to be called.</param>
        /// <param name="arguments">Method call arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Call(Expression instance, MethodInfo method, params Expression[] arguments)
            => Call(instance, method, (IEnumerable<Expression>)arguments);

        /// <summary>
        /// Adds instance method call statement.
        /// </summary>
        /// <param name="instance"><see langword="this"/> argument.</param>
        /// <param name="methodName">The method to be called.</param>
        /// <param name="arguments">Method call arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Call(Expression instance, string methodName, params Expression[] arguments)
            => CurrentScope.AddStatement(instance.Call(methodName, arguments));

        /// <summary>
        /// Adds static method call statement.,
        /// </summary>
        /// <param name="method">The method to be called.</param>
        /// <param name="arguments">Method call arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Call(MethodInfo method, IEnumerable<Expression> arguments)
            => CurrentScope.AddStatement(Expression.Call(null, method, arguments));

        /// <summary>
        /// Adds static method call statement.
        /// </summary>
        /// <param name="method">The method to be called.</param>
        /// <param name="arguments">Method call arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Call(MethodInfo method, params Expression[] arguments)
            => Call(method, (IEnumerable<Expression>)arguments);

        /// <summary>
        /// Adds static method call.
        /// </summary>
        /// <param name="type">The type that declares static method.</param>
        /// <param name="methodName">The name of the static method.</param>
        /// <param name="arguments">The arguments to be passed into static method.</param>
        /// <returns>An expression representing static method call.</returns>
        public static void CallStatic(Type type, string methodName, params Expression[] arguments)
            => CurrentScope.AddStatement(type.CallStatic(methodName, arguments));

        /// <summary>
        /// Constructs static method call.
        /// </summary>
        /// <typeparam name="T">The type that declares static method.</typeparam>
        /// <param name="methodName">The name of the static method.</param>
        /// <param name="arguments">The arguments to be passed into static method.</param>
        /// <returns>An expression representing static method call.</returns>
        public static void CallStatic<T>(string methodName, params Expression[] arguments)
            => CallStatic(typeof(T), methodName, arguments);

        /// <summary>
        /// Declares label of the specified type.
        /// </summary>
        /// <param name="type">The type of landing site.</param>
        /// <param name="name">The optional name of the label.</param>
        /// <returns>Declared label.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static LabelTarget Label(Type type, string name = null)
        {
            var target = Expression.Label(type, name);
            Label(target);
            return target;
        }

        /// <summary>
        /// Declares label of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of landing site.</typeparam>
        /// <param name="name">The optional name of the label.</param>
        /// <returns>Declared label.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static LabelTarget Label<T>(string name = null) => Label(typeof(T), name);

        /// <summary>
        /// Declares label in the current scope.
        /// </summary>
        /// <returns>Declared label.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static LabelTarget Label() => Label(typeof(void));

        /// <summary>
        /// Adds label landing site to this scope.
        /// </summary>
        /// <param name="target">The label target.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Label(LabelTarget target) => CurrentScope.AddStatement(Expression.Label(target));

        private static void Goto(LabelTarget target, Expression value, GotoExpressionKind kind)
            => CurrentScope.AddStatement(Expression.MakeGoto(kind, target, value, value?.Type ?? typeof(void)));

        /// <summary>
        /// Adds unconditional control transfer statement to this scope.
        /// </summary>
        /// <param name="target">The label reference.</param>
        /// <param name="value">The value to be associated with the control transfer.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Goto(LabelTarget target, Expression value) => Goto(target, value, GotoExpressionKind.Goto);

        /// <summary>
        /// Adds unconditional control transfer statement to this scope.
        /// </summary>
        /// <param name="target">The label reference.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Goto(LabelTarget target) => Goto(target, null);

        /// <summary>
        /// Declares local variable in the current lexical scope.
        /// </summary>
        /// <typeparam name="T">The type of local variable.</typeparam>
        /// <param name="name">The name of local variable.</param>
        /// <returns>The expression representing local variable.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static ParameterExpression DeclareVariable<T>(string name) => DeclareVariable(typeof(T), name);

        /// <summary>
        /// Declares local variable in the current lexical scope. 
        /// </summary>
        /// <param name="variableType">The type of local variable.</param>
        /// <param name="name">The name of local variable.</param>
        /// <returns>The expression representing local variable.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static ParameterExpression DeclareVariable(Type variableType, string name)
        {
            var variable = Expression.Variable(variableType, name);
            CurrentScope.DeclareVariable(variable);
            return variable;
        }

        /// <summary>
        /// Declares initialized local variable of automatically
        /// inferred type.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>var i = expr;</c>
        /// </remarks>
        /// <param name="name">The name of the variable.</param>
        /// <param name="init">Initialization expression.</param>
        /// <returns>The expression representing local variable.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static ParameterExpression DeclareVariable(string name, Expression init)
        {
            var variable = DeclareVariable(init.Type, name);
            Assign(variable, init);
            return variable;
        }

        /// <summary>
        /// Adds await operator.
        /// </summary>
        /// <param name="asyncResult">The expression representing asynchronous computing process.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Await(Expression asyncResult) => CurrentScope.AddStatement(asyncResult.Await());

        internal static ConditionalBuilder MakeConditional(Expression test) => new ConditionalBuilder(MakeScope, test);

        /// <summary>
        /// Adds if-then-else statement to this scope.
        /// </summary>
        /// <param name="test">Test expression.</param>
        /// <returns>Conditional statement builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static ConditionalBuilder If(Expression test)
            => MakeConditional(test).TreatAsStatement<ConditionalExpression, ConditionalBuilder>();

        /// <summary>
        /// Adds if-then statement to this scope.
        /// </summary>
        /// <param name="test">Test expression.</param>
        /// <param name="ifTrue">Positive branch builder.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void IfThen(Expression test, Action ifTrue)
            => If(test).Then(ifTrue).End();

        /// <summary>
        /// Adds if-then-else statement to this scope.
        /// </summary>
        /// <param name="test">Test expression.</param>
        /// <param name="ifTrue">Positive branch builder.</param>
        /// <param name="ifFalse">Negative branch builder.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void IfThenElse(Expression test, Action ifTrue, Action ifFalse)
            => If(test).Then(ifTrue).Else(ifFalse).End();

        private readonly struct WhileLoopFactory : ILexicalScopeFactory<WhileLoopScope>
        {
            private readonly bool checkConditionFirst;
            private readonly Expression test;

            internal WhileLoopFactory(Expression test, bool checkConditionFirst)
            {
                this.checkConditionFirst = checkConditionFirst;
                this.test = test;
            }

            WhileLoopScope ILexicalScopeFactory<WhileLoopScope>.CreateScope(LexicalScope parent) => new WhileLoopScope(test, parent, checkConditionFirst);
        }

        /// <summary>
        /// Adds <see langword="while"/> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void While(Expression test, Action<LoopContext> body)
            => AddStatement<Action<LoopContext>, WhileLoopScope, WhileLoopFactory>(new WhileLoopFactory(test, true), body);

        /// <summary>
        /// Adds <see langword="while"/> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void While(Expression test, Action body)
            => AddStatement<Action, WhileLoopScope, WhileLoopFactory>(new WhileLoopFactory(test, true), body);

        /// <summary>
        /// Adds <c>do{ } while(condition);</c> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void DoWhile(Expression test, Action<LoopContext> body)
            => AddStatement<Action<LoopContext>, WhileLoopScope, WhileLoopFactory>(new WhileLoopFactory(test, false), body);

        /// <summary>
        /// Adds <c>do{ } while(condition);</c> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void DoWhile(Expression test, Action body)
            => AddStatement<Action, WhileLoopScope, WhileLoopFactory>(new WhileLoopFactory(test, false), body);

        private readonly struct ForEachLoopScopeFactory : ILexicalScopeFactory<ForEachLoopScope>
        {
            private readonly Expression collection;

            internal ForEachLoopScopeFactory(Expression collection) => this.collection = collection;

            ForEachLoopScope ILexicalScopeFactory<ForEachLoopScope>.CreateScope(LexicalScope parent) => new ForEachLoopScope(collection, parent);
        }

        /// <summary>
        /// Adds <see langword="foreach"/> loop statement.
        /// </summary>
        /// <param name="collection">The expression providing enumerable collection.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/foreach-in">foreach Statement</seealso>
        public static void ForEach(Expression collection, Action<MemberExpression, LoopContext> body)
            => AddStatement<Action<MemberExpression, LoopContext>, ForEachLoopScope, ForEachLoopScopeFactory>(new ForEachLoopScopeFactory(collection), body);

        /// <summary>
        /// Adds <see langword="foreach"/> loop statement.
        /// </summary>
        /// <param name="collection">The expression providing enumerable collection.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/foreach-in">foreach Statement</seealso>
        public static void ForEach(Expression collection, Action<MemberExpression> body)
            => AddStatement<Action<MemberExpression>, ForEachLoopScope, ForEachLoopScopeFactory>(new ForEachLoopScopeFactory(collection), body);

        private readonly struct ForLoopScopeFactory : ILexicalScopeFactory<ForLoopScope>
        {
            private readonly Expression initializer;
            private readonly Func<ParameterExpression, Expression> condition;

            internal ForLoopScopeFactory(Expression initializer, Func<ParameterExpression, Expression> condition)
            {
                this.initializer = initializer;
                this.condition = condition;
            }

            ForLoopScope ILexicalScopeFactory<ForLoopScope>.CreateScope(LexicalScope parent) => new ForLoopScope(initializer, condition, parent);
        }

        /// <summary>
        /// Adds <see langword="for"/> loop statement.
        /// </summary>
        /// <remarks>
        /// This builder constructs the statement equivalent to <c>for(var i = initializer; condition; iter){ body; }</c>
        /// </remarks>
        /// <param name="initializer">Loop variable initialization expression.</param>
        /// <param name="condition">Loop continuation condition.</param>
        /// <param name="iteration">Iteration statements.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/for">for Statement</seealso>
        public static void For(Expression initializer, Func<ParameterExpression, Expression> condition, Action<ParameterExpression> iteration, Action<ParameterExpression, LoopContext> body)
            => AddStatement<Action<ForLoopScope>, ForLoopScope, ForLoopScopeFactory>(new ForLoopScopeFactory(initializer, condition), scope => scope.ConstructBody(body, iteration));

        /// <summary>
        /// Adds <see langword="for"/> loop statement.
        /// </summary>
        /// <remarks>
        /// This builder constructs the statement equivalent to <c>for(var i = initializer; condition; iter){ body; }</c>
        /// </remarks>
        /// <param name="initializer">Loop variable initialization expression.</param>
        /// <param name="condition">Loop continuation condition.</param>
        /// <param name="iteration">Iteration statements.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/for">for Statement</seealso>
        public static void For(Expression initializer, Func<ParameterExpression, Expression> condition, Action<ParameterExpression> iteration, Action<ParameterExpression> body)
            => AddStatement<Action<ForLoopScope>, ForLoopScope, ForLoopScopeFactory>(new ForLoopScopeFactory(initializer, condition), scope => scope.ConstructBody(body, iteration));

        private readonly struct LoopScopeFactory : ILexicalScopeFactory<LoopScope>
        {
            LoopScope ILexicalScopeFactory<LoopScope>.CreateScope(LexicalScope parent) => new LoopScope(parent);
        }

        /// <summary>
        /// Adds generic loop statement.
        /// </summary>
        /// <remarks>
        /// This loop is equvalent to <c>while(true){ }</c>
        /// </remarks>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Loop(Action<LoopContext> body)
            => AddStatement<Action<LoopContext>, LoopScope, LoopScopeFactory>(new LoopScopeFactory(), body);

        /// <summary>
        /// Adds generic loop statement.
        /// </summary>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Loop(Action body) => AddStatement<Action, LoopScope, LoopScopeFactory>(new LoopScopeFactory(), body);

        /// <summary>
        /// Adds <see langword="throw"/> statement to the compound statement.
        /// </summary>
        /// <param name="exception">The exception to be thrown.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Throw(Expression exception) => CurrentScope.AddStatement(Expression.Throw(exception));

        /// <summary>
        /// Adds <see langword="throw"/> statement to the compound statement.
        /// </summary>
        /// <typeparam name="E">The exception to be thrown.</typeparam>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Throw<E>() where E : Exception, new() => Throw(Expression.New(typeof(E).GetConstructor(Array.Empty<Type>())));

        /// <summary>
        /// Adds re-throw statement.
        /// </summary>
        public static void Rethrow() => CurrentScope.AddStatement(Expression.Rethrow());

        private readonly struct UsingBlockScopeFactory : ILexicalScopeFactory<UsingBlockScope>
        {
            private readonly Expression resource;

            internal UsingBlockScopeFactory(Expression resource) => this.resource = resource;

            UsingBlockScope ILexicalScopeFactory<UsingBlockScope>.CreateScope(LexicalScope parent) => new UsingBlockScope(resource, parent);
        }

        internal static Expression MakeUsing(Expression resource, Action<ParameterExpression> body)
            => Build<Expression, Action<ParameterExpression>, UsingBlockScope, UsingBlockScopeFactory>(new UsingBlockScopeFactory(resource), body);

        internal static Expression MakeUsing(Expression resource, Action body)
            => Build<Expression, Action, UsingBlockScope, UsingBlockScopeFactory>(new UsingBlockScopeFactory(resource), body);

        /// <summary>
        /// Adds <see langword="using"/> statement.
        /// </summary>
        /// <param name="resource">The expression representing disposable resource.</param>
        /// <param name="body">The body of the statement.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement">using Statement</seealso>
        public static void Using(Expression resource, Action<ParameterExpression> body)
            => AddStatement<Action<ParameterExpression>, UsingBlockScope, UsingBlockScopeFactory>(new UsingBlockScopeFactory(resource), body);

        /// <summary>
        /// Adds <see langword="using"/> statement.
        /// </summary>
        /// <param name="resource">The expression representing disposable resource.</param>
        /// <param name="body">The body of the statement.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement">using Statement</seealso>
        public static void Using(Expression resource, Action body)
            => AddStatement<Action, UsingBlockScope, UsingBlockScopeFactory>(new UsingBlockScopeFactory(resource), body);

        private readonly struct LockScopeFactory : ILexicalScopeFactory<LockScope>
        {
            private readonly Expression syncRoot;

            internal LockScopeFactory(Expression syncRoot) => this.syncRoot = syncRoot;

            LockScope ILexicalScopeFactory<LockScope>.CreateScope(LexicalScope parent) => new LockScope(syncRoot, parent);
        }

        internal static BlockExpression MakeLock(Expression syncRoot, Action<ParameterExpression> body)
            => Build<BlockExpression, Action<ParameterExpression>, LockScope, LockScopeFactory>(new LockScopeFactory(syncRoot), body);

        internal static BlockExpression MakeLock(Expression syncRoot, Action body)
            => Build<BlockExpression, Action, LockScope, LockScopeFactory>(new LockScopeFactory(syncRoot), body);

        /// <summary>
        /// Adds <see langword="lock"/> statement.
        /// </summary>
        /// <param name="syncRoot">The object to be locked during execution of the compound statement.</param>
        /// <param name="body">Synchronized scope of code.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement">lock Statement</seealso>
        public static void Lock(Expression syncRoot, Action<ParameterExpression> body)
            => AddStatement<Action<ParameterExpression>, LockScope, LockScopeFactory>(new LockScopeFactory(syncRoot), body);

        /// <summary>
        /// Adds <see langword="lock"/> statement.
        /// </summary>
        /// <param name="syncRoot">The object to be locked during execution of the compound statement.</param>
        /// <param name="body">Synchronized scope of code.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement">lock Statement</seealso>
        public static void Lock(Expression syncRoot, Action body)
            => AddStatement<Action, LockScope, LockScopeFactory>(new LockScopeFactory(syncRoot), body);

        private readonly struct WithBlockScopeFactory : ILexicalScopeFactory<WithBlockScope>
        {
            private readonly Expression expression;

            internal WithBlockScopeFactory(Expression expression) => this.expression = expression;

            WithBlockScope ILexicalScopeFactory<WithBlockScope>.CreateScope(LexicalScope parent) => new WithBlockScope(expression, parent);
        }

        internal static Expression MakeWith(Expression expression, Action<ParameterExpression> body)
            => Build<Expression, Action<ParameterExpression>, WithBlockScope, WithBlockScopeFactory>(new WithBlockScopeFactory(expression), body);

        /// <summary>
        /// Constructs compound statement hat repeatedly refer to a single object or 
        /// structure so that the statements can use a simplified syntax when accessing members 
        /// of the object or structure.
        /// </summary>
        /// <param name="expression">The implicitly referenced object.</param>
        /// <param name="body">The statement body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/visual-basic/language-reference/statements/with-end-with-statement">With..End Statement</seealso>
        public static void With(Expression expression, Action<ParameterExpression> body)
            => AddStatement<Action<ParameterExpression>, WithBlockScope, WithBlockScopeFactory>(new WithBlockScopeFactory(expression), body);

        internal static SwitchBuilder MakeSwitch(Expression switchValue) => new SwitchBuilder(MakeScope, switchValue);

        /// <summary>
        /// Adds selection expression.
        /// </summary>
        /// <param name="switchValue">The value to be handled by the selection expression.</param>
        /// <returns>Selection expression builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static SwitchBuilder Switch(Expression switchValue) => MakeSwitch(switchValue).TreatAsStatement<SwitchExpression, SwitchBuilder>();

        internal static TryBuilder MakeTry(Expression body) => new TryBuilder(MakeScope, body);

        /// <summary>
        /// Adds structured exception handling statement.
        /// </summary>
        /// <param name="body"><see langword="try"/> block.</param>
        /// <returns>Structured exception handling builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static TryBuilder Try(Expression body) => MakeTry(body).TreatAsStatement<TryExpression, TryBuilder>();

        /// <summary>
        /// Adds structured exception handling statement.
        /// </summary>
        /// <param name="scope"><see langword="try"/> block builder.</param>
        /// <returns>Structured exception handling builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static TryBuilder Try(Action scope) => Try(MakeScope(scope));

        private readonly struct LocalScopeFactory : ILexicalScopeFactory<LocalScope>
        {
            LocalScope ILexicalScopeFactory<LocalScope>.CreateScope(LexicalScope parent) => new LocalScope(parent);
        }

        private static Expression MakeScope(Action body)
            => Build<Expression, Action, LocalScope, LocalScopeFactory>(new LocalScopeFactory(), body);

        private static LabelTarget ContinueLabel(LoopScopeBase scope) => scope.ContinueLabel;

        private static LabelTarget BreakLabel(LoopScopeBase scope) => scope.BreakLabel;

        private static void Goto(LoopContext loop, Func<LoopScopeBase, LabelTarget> labelFactory, GotoExpressionKind kind)
        {
            if (loop.TryGetScope(out var scope))
                Goto(labelFactory(scope), null, kind);
            else
                throw new ArgumentException(ExceptionMessages.LoopNotAvailable, nameof(loop));
        }

        private static void Goto(Func<LoopScopeBase, LabelTarget> labelFactory, GotoExpressionKind kind)
        {
            var loop = FindScope<LoopScopeBase>() ?? throw new InvalidOperationException(ExceptionMessages.LoopNotAvailable);
            Goto(labelFactory(loop), null, kind);
        }

        /// <summary>
        /// Restarts execution of the loop.
        /// </summary>
        /// <param name="loop">The loop reference.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Continue(LoopContext loop) => Goto(loop, ContinueLabel, GotoExpressionKind.Continue);

        /// <summary>
        /// Restarts execution of the entire loop.
        /// </summary>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Continue() => Goto(ContinueLabel, GotoExpressionKind.Continue);

        /// <summary>
        /// Stops execution the specified loop.
        /// </summary>
        /// <param name="loop">The loop reference.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Break(LoopContext loop) => Goto(loop, BreakLabel, GotoExpressionKind.Break);

        /// <summary>
        /// Stops execution of the entire loop.
        /// </summary>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Break() => Goto(BreakLabel, GotoExpressionKind.Break);

        /// <summary>
        /// Adds <see langword="return"/> instruction to return from
        /// underlying lambda function having non-<see langword="void"/>
        /// return type.
        /// </summary>
        /// <param name="result">Optional value to be returned from the lambda function.</param>
        /// <exception cref="InvalidOperationException">This method is not called from within body of lambda function.</exception>
        public static void Return(Expression result = null)
        {
            var lambda = FindScope<LambdaScope>() ?? throw new InvalidOperationException(ExceptionMessages.CallFromLambdaExpected);
            CurrentScope.AddStatement(lambda.Return(result));
        }

        private readonly struct LambdaScopeFactory<D> : ILexicalScopeFactory<LambdaScope<D>>
            where D : Delegate
        {
            private readonly bool tailCall;

            internal LambdaScopeFactory(bool tailCall) => this.tailCall = tailCall;

            LambdaScope<D> ILexicalScopeFactory<LambdaScope<D>>.CreateScope(LexicalScope parent) => new LambdaScope<D>(parent, tailCall);
        }

        /// <summary>
        /// Constructs lamdba function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="D">The delegate describing signature of lambda function.</typeparam>
        /// <param name="tailCall"><see langword="true"/> if the lambda expression will be compiled with the tail call optimization, otherwise <see langword="false"/>.</param>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        public static Expression<D> Lambda<D>(bool tailCall, Action<LambdaContext> body)
            where D : Delegate
            => Build<Expression<D>, Action<LambdaContext>, LambdaScope<D>, LambdaScopeFactory<D>>(new LambdaScopeFactory<D>(tailCall), body);

        /// <summary>
        /// Constructs lamdba function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="D">The delegate describing signature of lambda function.</typeparam>
        /// <param name="tailCall"><see langword="true"/> if the lambda expression will be compiled with the tail call optimization, otherwise <see langword="false"/>.</param>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        public static Expression<D> Lambda<D>(bool tailCall, Action<LambdaContext, ParameterExpression> body)
            where D : Delegate
            => Build<Expression<D>, Action<LambdaContext, ParameterExpression>, LambdaScope<D>, LambdaScopeFactory<D>>(new LambdaScopeFactory<D>(tailCall), body);

        /// <summary>
        /// Constructs lamdba function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="D">The delegate describing signature of lambda function.</typeparam>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        public static Expression<D> Lambda<D>(Action<LambdaContext> body)
            where D : Delegate
            => Lambda<D>(false, body);

        /// <summary>
        /// Constructs lamdba function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="D">The delegate describing signature of lambda function.</typeparam>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        public static Expression<D> Lambda<D>(Action<LambdaContext, ParameterExpression> body)
            where D : Delegate
            => Lambda<D>(false, body);

        private readonly struct AsyncLambdaScopeFactory<D> : ILexicalScopeFactory<AsyncLambdaScope<D>>
            where D : Delegate
        {
            AsyncLambdaScope<D> ILexicalScopeFactory<AsyncLambdaScope<D>>.CreateScope(LexicalScope parent) => new AsyncLambdaScope<D>(parent);
        }

        /// <summary>
        /// Constructs async lambda function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="D">The delegate describing signature of lambda function.</typeparam>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        /// <seealso cref="AwaitExpression"/>
        /// <seealso cref="AsyncResultExpression"/>
        /// <seealso cref="AsyncLambdaScope{D}"/>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/#BKMK_HowtoWriteanAsyncMethod">Async methods</seealso>
        public static Expression<D> AsyncLambda<D>(Action<LambdaContext> body)
            where D : Delegate
            => Build<Expression<D>, Action<LambdaContext>, AsyncLambdaScope<D>, AsyncLambdaScopeFactory<D>>(new AsyncLambdaScopeFactory<D>(), body);
    }
}