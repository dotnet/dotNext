using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;

    /// <summary>
    /// Represents code generator.
    /// </summary>
    public static class CodeGenerator
    {
        [ThreadStatic]
        private static LexicalScope current;

        private static S FindScope<S>()
            where S : LexicalScope
        {
            for(var current = CodeGenerator.current; !(current is null); current = current.Parent)
                if(current is S scope)
                    return scope;
            return null;
        }

        private static E InitStatement<E, D, S, F>(F factory, D action)
            where E : class
            where D : MulticastDelegate
            where S : LexicalScope, ILexicalScope<E, D>
            where F : LexicalScope.IFactory<S>
        {
            E expression;
            S statement;
            current = statement = factory.Create(current);
            try
            {
                expression = statement.Build(action);
            }
            finally
            {
                current = current?.Parent;
                statement.Dispose();
            }
            return expression;
        }

        private static void AddStatement<D, S, F>(F factory, D action)
            where D : MulticastDelegate
            where S : LexicalScope, ILexicalScope<Expression, D>
            where F : LexicalScope.IFactory<S>
            => CurrentScope.AddStatement(InitStatement<Expression, D, S, F>(factory, action));

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

        public static ConditionalBuilder If(Expression test) => new ConditionalBuilder(test, CurrentScope);

        public static ConditionalBuilder Then(this ConditionalBuilder builder, Action body)
            => InitStatement<ConditionalBuilder, Action, BranchStatement, BranchStatement.Factory>(new BranchStatement.Factory(builder, true), body);

        public static ConditionalBuilder Else(this ConditionalBuilder builder, Action body)
            => InitStatement<ConditionalBuilder, Action, BranchStatement, BranchStatement.Factory>(new BranchStatement.Factory(builder, false), body);

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

        /// <summary>
        /// Adds <see langword="while"/> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/while">while Statement</seealso>
        public static void While(Expression test, Action<LoopContext> body)
            => AddStatement<Action<LoopContext>, WhileStatement, WhileStatement.Factory>(new WhileStatement.Factory(test, true), body);

        /// <summary>
        /// Adds <see langword="while"/> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/while">while Statement</seealso>
        public static void While(Expression test, Action body)
            => AddStatement<Action, WhileStatement, WhileStatement.Factory>(new WhileStatement.Factory(test, true), body);

        /// <summary>
        /// Adds <c>do{ } while(condition);</c> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/do">do-while Statement</seealso>
        public static void DoWhile(Expression test, Action<LoopContext> body)
            => AddStatement<Action<LoopContext>, WhileStatement, WhileStatement.Factory>(new WhileStatement.Factory(test, false), body);

        /// <summary>
        /// Adds <c>do{ } while(condition);</c> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/do">do-while Statement</seealso>
        public static void DoWhile(Expression test, Action body)
            => AddStatement<Action, WhileStatement, WhileStatement.Factory>(new WhileStatement.Factory(test, false), body);

        /// <summary>
        /// Adds <see langword="foreach"/> loop statement.
        /// </summary>
        /// <param name="collection">The expression providing enumerable collection.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/foreach-in">foreach Statement</seealso>
        public static void ForEach(Expression collection, Action<MemberExpression, LoopContext> body)
            => AddStatement<Action<MemberExpression, LoopContext>, ForEachStatement, ForEachStatement.Factory>(new ForEachStatement.Factory(collection), body);

        /// <summary>
        /// Adds <see langword="foreach"/> loop statement.
        /// </summary>
        /// <param name="collection">The expression providing enumerable collection.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/foreach-in">foreach Statement</seealso>
        public static void ForEach(Expression collection, Action<MemberExpression> body)
            => AddStatement<Action<MemberExpression>, ForEachStatement, ForEachStatement.Factory>(new ForEachStatement.Factory(collection), body);

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
            => AddStatement<Action<ParameterExpression, LoopContext>, ForStatement, ForStatement.Factory>(new ForStatement.Factory(initializer, condition, iteration), body);

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
            => AddStatement<Action<ParameterExpression>, ForStatement, ForStatement.Factory>(new ForStatement.Factory(initializer, condition, iteration), body);

        /// <summary>
        /// Adds generic loop statement.
        /// </summary>
        /// <remarks>
        /// This loop is equvalent to <c>while(true){ }</c>
        /// </remarks>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Loop(Action<LoopContext> body) 
            => AddStatement<Action<LoopContext>, LoopStatement, LexicalScope.IFactory<LoopStatement>>(LoopStatement.Factory, body);

        /// <summary>
        /// Adds generic loop statement.
        /// </summary>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Loop(Action body) 
            => AddStatement<Action, LoopStatement, LexicalScope.IFactory<LoopStatement>>(LoopStatement.Factory, body);

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

        /// <summary>
        /// Adds <see langword="using"/> statement.
        /// </summary>
        /// <param name="resource">The expression representing disposable resource.</param>
        /// <param name="body">The body of the statement.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement">using Statement</seealso>
        public static void Using(Expression resource, Action<ParameterExpression> body)
            => AddStatement<Action<ParameterExpression>, UsingStatement, UsingStatement.Factory>(new UsingStatement.Factory(resource), body);

        /// <summary>
        /// Adds <see langword="using"/> statement.
        /// </summary>
        /// <param name="resource">The expression representing disposable resource.</param>
        /// <param name="body">The body of the statement.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement">using Statement</seealso>
        public static void Using(Expression resource, Action body)
            => AddStatement<Action, UsingStatement, UsingStatement.Factory>(new UsingStatement.Factory(resource), body);

        /// <summary>
        /// Adds <see langword="lock"/> statement.
        /// </summary>
        /// <param name="syncRoot">The object to be locked during execution of the compound statement.</param>
        /// <param name="body">Synchronized scope of code.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement">lock Statement</seealso>
        public static void Lock(Expression syncRoot, Action<ParameterExpression> body)
            => AddStatement<Action<ParameterExpression>, LockStatement, LockStatement.Factory>(new LockStatement.Factory(syncRoot), body);

        /// <summary>
        /// Adds <see langword="lock"/> statement.
        /// </summary>
        /// <param name="syncRoot">The object to be locked during execution of the compound statement.</param>
        /// <param name="body">Synchronized scope of code.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement">lock Statement</seealso>
        public static void Lock(Expression syncRoot, Action body)
            => AddStatement<Action, LockStatement, LockStatement.Factory>(new LockStatement.Factory(syncRoot), body);

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
            => AddStatement<Action<ParameterExpression>, WithStatement, WithStatement.Factory>(new WithStatement.Factory(expression), body);

        public static SwitchBuilder Switch(Expression value) => new SwitchBuilder(value, CurrentScope);

        /// <summary>
        /// Specifies a pattern to compare to the match expression
        /// and action to be executed if matching is successful.
        /// </summary>
        /// <param name="testValues">A list of test values.</param>
        /// <param name="body">The block code to be executed if input value is equal to one of test values.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public static SwitchBuilder Case(this SwitchBuilder builder, IEnumerable<Expression> testValues, Action body)
            => InitStatement<SwitchBuilder, Action, CaseStatement, CaseStatement.Factory>(new CaseStatement.Factory(builder, testValues), body);


        /// <summary>
        /// Specifies a pattern to compare to the match expression
        /// and action to be executed if matching is successful.
        /// </summary>
        /// <param name="test">Single test value.</param>
        /// <param name="body">The block code to be executed if input value is equal to one of test values.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public static SwitchBuilder Case(this SwitchBuilder builder, Expression test, Action body)
            => Case(builder, Sequence.Singleton(test), body);

        /// <summary>
        /// Specifies the switch section to execute if the match expression
        /// doesn't match any other cases.
        /// </summary>
        /// <param name="body">The block code to be executed if input value is equal to one of test values.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public static SwitchBuilder Default(this SwitchBuilder builder, Action body)
            => InitStatement<SwitchBuilder, Action, DefaultStatement, DefaultStatement.Factory>(new DefaultStatement.Factory(builder), body);

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <param name="exceptionType">Expected exception.</param>
        /// <param name="filter">Additional filter to be applied to the caught exception.</param>
        /// <param name="handler">Exception handling block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public static TryBuilder Catch(this TryBuilder builder, Type exceptionType, TryBuilder.Filter filter, Action<ParameterExpression> handler)
            => InitStatement<TryBuilder, Action<ParameterExpression>, CatchStatement, CatchStatement.Factory>(new CatchStatement.Factory(builder, exceptionType, filter), handler);

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <param name="exceptionType">Expected exception.</param>
        /// <param name="handler">Exception handling block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public static TryBuilder Catch(this TryBuilder builder, Type exceptionType, Action<ParameterExpression> handler)
            => Catch(builder, exceptionType, null, handler);

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <typeparam name="E">Expected exception.</typeparam>
        /// <param name="handler">Exception handling block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public static TryBuilder Catch<E>(this TryBuilder builder, Action<ParameterExpression> handler)
            where E : Exception
            => Catch(builder, typeof(E), handler);

        public static TryBuilder Catch(this TryBuilder builder, Action handler)
            => InitStatement<TryBuilder, Action, CatchStatement, CatchStatement.Factory>(new CatchStatement.Factory(builder), handler);

        /// <summary>
        /// Constructs block of code which will be executed in case
        /// of any exception.
        /// </summary>
        /// <param name="fault">Fault handling block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public static TryBuilder Fault(this TryBuilder builder, Action fault)
            => InitStatement<TryBuilder, Action, FaultStatement, FaultStatement.Factory>(new FaultStatement.Factory(builder), fault);

        /// <summary>
        /// Adds structured exception handling statement.
        /// </summary>
        /// <param name="scope"><see langword="try"/> block builder.</param>
        /// <returns>Structured exception handling builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static TryBuilder Try(Action scope) 
            => InitStatement<TryBuilder, Action, TryStatement, LexicalScope.IFactory<TryStatement>>(TryStatement.Factory, scope);

        /// <summary>
        /// Constructs block of code run when control leaves a <see langword="try"/> statement.
        /// </summary>
        /// <param name="body">The block of code to be executed.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public static TryBuilder Finally(this TryBuilder builder, Action body) 
            => InitStatement<TryBuilder, Action, FinallyStatement, FinallyStatement.Factory>(new FinallyStatement.Factory(builder), body); 

        /// <summary>
        /// Restarts execution of the loop.
        /// </summary>
        /// <param name="loop">The loop reference.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Continue(LoopContext loop) => Goto(loop.ContinueLabel, null, GotoExpressionKind.Continue);

        /// <summary>
        /// Restarts execution of the entire loop.
        /// </summary>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Continue()
        {
            var loop = FindScope<LoopLexicalScope>() ?? throw new InvalidOperationException(ExceptionMessages.LoopNotAvailable);
            Continue(new LoopContext(loop));
        }

        /// <summary>
        /// Stops execution the specified loop.
        /// </summary>
        /// <param name="loop">The loop reference.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Break(LoopContext loop) => Goto(loop.BreakLabel, null, GotoExpressionKind.Break);

        /// <summary>
        /// Stops execution of the entire loop.
        /// </summary>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Break()
        {
            var loop = FindScope<LoopLexicalScope>() ?? throw new InvalidOperationException(ExceptionMessages.LoopNotAvailable);
            Break(new LoopContext(loop));
        }

        /// <summary>
        /// Adds <see langword="return"/> instruction to return from
        /// underlying lambda function having non-<see langword="void"/>
        /// return type.
        /// </summary>
        /// <param name="result">Optional value to be returned from the lambda function.</param>
        /// <exception cref="InvalidOperationException">This method is not called from within body of lambda function.</exception>
        public static void Return(Expression result = null)
        {
            var lambda = FindScope<LambdaExpression>() ?? throw new InvalidOperationException(ExceptionMessages.CallFromLambdaExpected);
            CurrentScope.AddStatement(lambda.Return(result));
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
            => InitStatement<Expression<D>, Action<LambdaContext>, LambdaExpression<D>, LambdaExpression<D>.Factory>(new LambdaExpression<D>.Factory(tailCall), body);

        /// <summary>
        /// Constructs lamdba function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="D">The delegate describing signature of lambda function.</typeparam>
        /// <param name="tailCall"><see langword="true"/> if the lambda expression will be compiled with the tail call optimization, otherwise <see langword="false"/>.</param>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        public static Expression<D> Lambda<D>(bool tailCall, Action<LambdaContext, ParameterExpression> body)
            where D : Delegate
            => InitStatement<Expression<D>, Action<LambdaContext, ParameterExpression>, LambdaExpression<D>, LambdaExpression<D>.Factory>(new LambdaExpression<D>.Factory(tailCall), body);

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

        /// <summary>
        /// Constructs async lambda function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="D">The delegate describing signature of lambda function.</typeparam>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        /// <seealso cref="AwaitExpression"/>
        /// <seealso cref="AsyncResultExpression"/>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/#BKMK_HowtoWriteanAsyncMethod">Async methods</seealso>
        public static Expression<D> AsyncLambda<D>(Action<LambdaContext> body)
            where D : Delegate
            => InitStatement<Expression<D>, Action<LambdaContext>, AsyncLambdaExpression<D>, LexicalScope.IFactory<AsyncLambdaExpression<D>>>(AsyncLambdaExpression<D>.Factory, body);
    }
}