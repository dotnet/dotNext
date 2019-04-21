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

        private static S PushScope<S>(Func<LexicalScope, S> factory) where S : LexicalScope
        {
            var scope = factory(current);
            current = scope;
            return scope;
        }

        private static void PopScope() => current = current?.Parent;

        /// <summary>
        /// Gets curremt lexical scope.
        /// </summary>
        internal static LexicalScope CurrentScope => current ?? throw new InvalidOperationException(ExceptionMessages.OutOfLexicalScope);

        /// <summary>
        /// Obtains local variable declared in the current or outer lexical scope.
        /// </summary>
        /// <param name="name">The name of the local variable.</param>
        /// <returns>Declared local variable; or <see langword="null"/>, if there is no declared local variable with the given name.</returns>
        public static ParameterExpression Variable(string name)
        {
            for(var current = CodeGenerator.current; !(current is null); current = current?.Parent)
                if(current.Variables.TryGetValue(name, out var variable))
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

        /// <summary>
        /// Adds unconditional control transfer statement to this scope.
        /// </summary>
        /// <param name="target">The label reference.</param>
        /// <param name="value">The value to be associated with the control transfer.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Goto(LabelTarget target, Expression value) => CurrentScope.AddStatement(Expression.Goto(target, value));

        /// <summary>
        /// Adds unconditional control transfer statement to this scope.
        /// </summary>
        /// <param name="target">The label reference.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Goto(LabelTarget target) => Goto(target, default);

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

        /// <summary>
        /// Adds if-then-else statement to this scope.
        /// </summary>
        /// <param name="test">Test expression.</param>
        /// <returns>Conditional statement builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static ConditionalBuilder If(Expression test) => new ConditionalBuilder(test, true);

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
        
        private static void AddStatement<S>(Func<LexicalScope, S> factory, Action<S> scopeHandler)
            where S : LexicalScope, IExpressionBuilder<Expression>
            => CurrentScope.AddStatement(Build<S, Expression>(factory, scopeHandler));

        private static E Build<S, E>(Func<LexicalScope, S> factory,  Action<S> scopeHandler)
            where E: Expression
            where S : LexicalScope, IExpressionBuilder<E>
        {
            var scope = PushScope(factory);
            E statement;
            try
            {
                scopeHandler(scope);
                statement = scope.Build();
            }
            finally
            {
                PopScope();
                scope.Dispose();
            }
            return statement;
        }

        private static void While(Expression test, bool checkConditionFirst, Action<LoopCookie> body)
            => AddStatement(parent => new WhileLoopBuider(test, parent, checkConditionFirst), scope => 
            {
                using(var cookie = new LoopCookie(scope))
                    body(cookie);
            });
        
        /// <summary>
        /// Adds <see langword="while"/> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void While(Expression test, Action<LoopCookie> body)
            => While(test, true, body);
        
        /// <summary>
        /// Adds <see langword="while"/> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void While(Expression test, Action body)
            => While(test, body.Parametrize<LoopCookie>());
        
        /// <summary>
        /// Adds <c>do{ } while(condition);</c> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void DoWhile(Expression test, Action<LoopCookie> body)
            => While(test, false, body);
        
        /// <summary>
        /// Adds <c>do{ } while(condition);</c> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void DoWhile(Expression test, Action body)
            => DoWhile(test, body.Parametrize<LoopCookie>());

        /// <summary>
        /// Adds <see langword="foreach"/> loop statement.
        /// </summary>
        /// <param name="collection">The expression providing enumerable collection.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/foreach-in">foreach Statement</seealso>
        public static void ForEach(Expression collection, Action<MemberExpression, LoopCookie> body)
            => AddStatement(parent => new ForEachLoopBuilder(collection, parent), scope => 
            {
                using(var cookie = new LoopCookie(scope))
                    body(scope.Element, cookie);
            });
        
        /// <summary>
        /// Adds <see langword="foreach"/> loop statement.
        /// </summary>
        /// <param name="collection">The expression providing enumerable collection.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/foreach-in">foreach Statement</seealso>
        public static void ForEach(Expression collection, Action<MemberExpression> body)
            => ForEach(collection, body.Parametrize<MemberExpression, LoopCookie>());
        
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
        public static void For(Expression initializer, Func<ParameterExpression, Expression> condition, Action<ParameterExpression> iteration, Action<LoopCookie> body)
            => AddStatement(parent => new ForLoopBuilder(initializer, condition, parent), scope => 
            {
                using(var cookie = new LoopCookie(scope))
                    body(cookie);
                scope.StartIterationCode();
                iteration(scope.LoopVar);
            });
        
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
        public static void For(Expression initializer, Func<ParameterExpression, Expression> condition, Action<ParameterExpression> iteration, Action body)
            => For(initializer, condition, iteration, body.Parametrize<LoopCookie>());
        
        /// <summary>
        /// Adds generic loop statement.
        /// </summary>
        /// <remarks>
        /// This loop is equvalent to <c>while(true){ }</c>
        /// </remarks>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Loop(Action<LoopCookie> body) 
            => AddStatement(parent => new LoopBuilder(parent), scope => 
            {
                using(var cookie = new LoopCookie(scope))
                    body(cookie);
            });
        
        /// <summary>
        /// Adds generic loop statement.
        /// </summary>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Loop(Action body) => Loop(body.Parametrize<LoopCookie>());

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
        /// Adds <see langword="using"/> statement.
        /// </summary>
        /// <param name="resource">The expression representing disposable resource.</param>
        /// <param name="body">The body of the statement.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement">using Statement</seealso>
        public static void Using(Expression resource, Action<ParameterExpression> body)
            => AddStatement(parent => new UsingBlockBuilder(resource, parent), scope => body(scope.Resource));
        
        /// <summary>
        /// Adds <see langword="using"/> statement.
        /// </summary>
        /// <param name="resource">The expression representing disposable resource.</param>
        /// <param name="body">The body of the statement.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement">using Statement</seealso>
        public static void Using(Expression resource, Action body)
            => Using(resource, body.Parametrize<ParameterExpression>());

        /// <summary>
        /// Adds <see langword="lock"/> statement.
        /// </summary>
        /// <param name="syncRoot">The object to be locked during execution of the compound statement.</param>
        /// <param name="body">Synchronized scope of code.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method for the outer scope from the inner scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement">lock Statement</seealso>
        public static void Lock(Expression syncRoot, Action<ParameterExpression> body)
            => AddStatement(parent => new LockBuilder(syncRoot, parent), scope => body(scope.SyncRoot));
        
        /// <summary>
        /// Adds <see langword="lock"/> statement.
        /// </summary>
        /// <param name="syncRoot">The object to be locked during execution of the compound statement.</param>
        /// <param name="body">Synchronized scope of code.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method for the outer scope from the inner scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement">lock Statement</seealso>
        public static void Lock(Expression syncRoot, Action body)
            => Lock(syncRoot, body.Parametrize<ParameterExpression>());
        
        /// <summary>
        /// Constructs compound statement hat repeatedly refer to a single object or 
        /// structure so that the statements can use a simplified syntax when accessing members 
        /// of the object or structure.
        /// </summary>
        /// <param name="expression">The implicitly referenced object.</param>
        /// <param name="body">The statement body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method for the outer scope from the inner scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/visual-basic/language-reference/statements/with-end-with-statement">With..End Statement</seealso>
        public static void With(UniversalExpression expression, Action<ParameterExpression> body)
            => AddStatement(parent => new WithBlockBuilder(expression, parent), scope => body(scope.Variable));
    }
}