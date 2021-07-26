using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;
    using LambdaExpressionTree = System.Linq.Expressions.LambdaExpression;
    using Seq = Collections.Generic.Sequence;
    using TaskType = Runtime.CompilerServices.TaskType;

    /// <summary>
    /// Represents code generator.
    /// </summary>
    public static class CodeGenerator
    {
        private static void Place<TDelegate, TStatement>(this TStatement statement, TDelegate scope)
            where TDelegate : MulticastDelegate
            where TStatement : Statement, ILexicalScope<Expression, TDelegate>
            => statement.Parent?.AddStatement(statement.Build(scope));

        /// <summary>
        /// Obtains local variable declared in the current or outer lexical scope.
        /// </summary>
        /// <param name="name">The name of the local variable.</param>
        /// <returns>Declared local variable; or <see langword="null"/>, if there is no declared local variable with the given name.</returns>
        public static ParameterExpression Variable(string name) => LexicalScope.Current[name];

        /// <summary>
        /// Adds no-operation instruction to this scope.
        /// </summary>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Nop() => Statement(Expression.Empty());

        /// <summary>
        /// Installs breakpoint.
        /// </summary>
        /// <remarks>
        /// This method installs breakpoint in DEBUG configuration.
        /// </remarks>
        [Conditional("DEBUG")]
        public static void Breakpoint() => Statement(ExpressionBuilder.Breakpoint());

        /// <summary>
        /// Writes line of the text into <see cref="Console.Out"/>.
        /// </summary>
        /// <param name="value">The value to be written into stdout.</param>
        public static void WriteLine(Expression value) => Statement(WriteLineExpression.Out(value));

        /// <summary>
        /// Writes line of the text into <see cref="Console.Error"/>.
        /// </summary>
        /// <param name="value">The value to be written into stderr.</param>
        public static void WriteError(Expression value) => Statement(WriteLineExpression.Error(value));

        /// <summary>
        /// Writes line of the text into attached debugger.
        /// </summary>
        /// <param name="value">The value to be written into attached debugger.</param>
        [Conditional("DEBUG")]
        public static void DebugMessage(Expression value) => Statement(WriteLineExpression.Debug(value));

        /// <summary>
        /// Checks for a condition; if the condition is false, displays a message box that shows the call stack.
        /// </summary>
        /// <param name="test">The conditional expression to evaluate. If the condition is <see langword="true"/>, the specified message is not sent and the message box is not displayed.</param>
        /// <param name="message">The message to include into trace.</param>
        [Conditional("DEBUG")]
        public static void Assert(Expression test, string? message = null)
            => Statement(test.Assert(message));

        /// <summary>
        /// Adds assignment operation to this scope.
        /// </summary>
        /// <param name="variable">The variable to modify.</param>
        /// <param name="value">The value to be assigned to the variable.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Assign(ParameterExpression variable, Expression value)
            => Statement(variable.Assign(value));

        /// <summary>
        /// Adds assignment operation to this scope.
        /// </summary>
        /// <param name="indexer">The indexer property or array element to modify.</param>
        /// <param name="value">The value to be assigned to the member or array element.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Assign(IndexExpression indexer, Expression value)
            => Statement(indexer.Assign(value));

        /// <summary>
        /// Adds assignment operation to this scope.
        /// </summary>
        /// <param name="member">The field or property to modify.</param>
        /// <param name="value">The value to be assigned to the member.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Assign(MemberExpression member, Expression value)
            => Statement(member.Assign(value));

        /// <summary>
        /// Adds an expression that increments given variable by 1 and assigns the result back to the variable.
        /// </summary>
        /// <param name="variable">The variable to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PreIncrementAssign(ParameterExpression variable)
            => Statement(variable.PreIncrementAssign());

        /// <summary>
        /// Adds an expression that represents the assignment of given variable followed by a subsequent increment by 1 of the original variable.
        /// </summary>
        /// <param name="variable">The variable to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PostIncrementAssign(ParameterExpression variable)
            => Statement(variable.PostIncrementAssign());

        /// <summary>
        /// Adds an expression that decrements given variable by 1 and assigns the result back to the variable.
        /// </summary>
        /// <param name="variable">The variable to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PreDecrementAssign(ParameterExpression variable)
            => Statement(variable.PreDecrementAssign());

        /// <summary>
        /// Adds an expression that represents the assignment of given variable followed by a subsequent decrement by 1 of the original variable.
        /// </summary>
        /// <param name="variable">The variable to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PostDecrementAssign(ParameterExpression variable)
            => Statement(variable.PostDecrementAssign());

        /// <summary>
        /// Adds an expression that increments given field or property by 1 and assigns the result back to the member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PreIncrementAssign(MemberExpression member)
            => Statement(member.PreIncrementAssign());

        /// <summary>
        /// Adds an expression that represents the assignment of given field or property followed by a subsequent increment by 1 of the original member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PostIncrementAssign(MemberExpression member)
            => Statement(member.PostIncrementAssign());

        /// <summary>
        /// Adds an expression that decrements given field or property by 1 and assigns the result back to the member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PreDecrementAssign(MemberExpression member)
            => Statement(member.PreDecrementAssign());

        /// <summary>
        /// Adds an expression that represents the assignment of given field or property followed by a subsequent decrement by 1 of the original member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PostDecrementAssign(MemberExpression member)
            => Statement(member.PostDecrementAssign());

        /// <summary>
        /// Adds an expression that increments given field or property by 1 and assigns the result back to the member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PreIncrementAssign(IndexExpression member)
            => Statement(member.PreIncrementAssign());

        /// <summary>
        /// Adds an expression that represents the assignment of given field or property followed by a subsequent increment by 1 of the original member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PostIncrementAssign(IndexExpression member)
            => Statement(member.PostIncrementAssign());

        /// <summary>
        /// Adds an expression that decrements given field or property by 1 and assigns the result back to the member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PreDecrementAssign(IndexExpression member)
            => Statement(member.PreDecrementAssign());

        /// <summary>
        /// Adds an expression that represents the assignment of given field or property followed by a subsequent decrement by 1 of the original member.
        /// </summary>
        /// <param name="member">The member to be modified.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void PostDecrementAssign(IndexExpression member)
            => Statement(member.PostDecrementAssign());

        /// <summary>
        /// Adds an expression that represents null-coalescing assignment of local variable.
        /// </summary>
        /// <param name="variable">The variable to be assigned.</param>
        /// <param name="right">The value to assigned to <paramref name="variable"/> if it is <see langword="null"/>.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <exception cref="ArgumentException"><paramref name="right"/> is not assignable to <paramref name="variable"/>.</exception>
        public static void NullCoalescingAssignment(ParameterExpression variable, Expression right)
            => Statement(variable.NullCoalescingAssignment(right));

        /// <summary>
        /// Adds an expression that represents null-coalescing assignment of property of field.
        /// </summary>
        /// <param name="member">The member to be assigned.</param>
        /// <param name="right">The value to assign to <paramref name="member"/> if it is <see langword="null"/>.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <exception cref="ArgumentException"><paramref name="right"/> is not assignable to <paramref name="member"/>.</exception>
        public static void NullCoalescingAssignment(MemberExpression member, Expression right)
            => Statement(member.NullCoalescingAssignment(right));

        /// <summary>
        /// Adds an expression that represents null-coalescing assignment of array element of indexer property.
        /// </summary>
        /// <param name="indexer">The indexer property or array element to be assigned.</param>
        /// <param name="right">The value to assign to <paramref name="indexer"/> if it is <see langword="null"/>.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <exception cref="ArgumentException"><paramref name="right"/> is not assignable to <paramref name="indexer"/>.</exception>
        public static void NullCoalescingAssignment(IndexExpression indexer, Expression right)
            => Statement(indexer.NullCoalescingAssignment(right));

        /// <summary>
        /// Adds constant as in-place statement.
        /// </summary>
        /// <typeparam name="T">The type of the constant.</typeparam>
        /// <param name="value">The value to be placed as statement.</param>
        public static void InPlaceValue<T>(T value)
            => Statement(value.Const());

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
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="instanceProperty">Instance property to be assigned.</param>
        /// <param name="value">A new value of the property.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Assign(Expression? instance, PropertyInfo instanceProperty, Expression value)
            => Statement(Expression.Assign(Expression.Property(instance, instanceProperty), value));

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
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="instanceField">Instance field to be assigned.</param>
        /// <param name="value">A new value of the field.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Assign(Expression? instance, FieldInfo instanceField, Expression value)
            => Statement(Expression.Assign(Expression.Field(instance, instanceField), value));

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
        public static void Invoke(Expression @delegate, IEnumerable<Expression> arguments)
            => Statement(Expression.Invoke(@delegate, arguments));

        /// <summary>
        /// Adds invocation statement which is not invoked if <paramref name="delegate"/> is <see langword="null"/>.
        /// </summary>
        /// <param name="delegate">The expression providing delegate to be invoked.</param>
        /// <param name="arguments">Delegate invocation arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void NullSafeInvoke(Expression @delegate, IEnumerable<Expression> arguments)
            => Statement(@delegate.IfNotNull(target => Expression.Invoke(target, arguments)));

        /// <summary>
        /// Adds invocation statement.
        /// </summary>
        /// <param name="delegate">The expression providing delegate to be invoked.</param>
        /// <param name="arguments">Delegate invocation arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Invoke(Expression @delegate, params Expression[] arguments) => Invoke(@delegate, (IEnumerable<Expression>)arguments);

        /// <summary>
        /// Adds invocation statement which is not invoked if <paramref name="delegate"/> is <see langword="null"/>.
        /// </summary>
        /// <param name="delegate">The expression providing delegate to be invoked.</param>
        /// <param name="arguments">Delegate invocation arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void NullSafeInvoke(Expression @delegate, params Expression[] arguments)
            => NullSafeInvoke(@delegate, (IEnumerable<Expression>)arguments);

        /// <summary>
        /// Inserts expression tree as a statement.
        /// </summary>
        /// <typeparam name="TDelegate">The type of the delegate describing lambda call site.</typeparam>
        /// <param name="lambda">The expression to be inserted as statement.</param>
        /// <param name="arguments">The arguments used to replace lambda parameters.</param>
        public static void Embed<TDelegate>(Expression<TDelegate> lambda, params Expression[] arguments)
            where TDelegate : MulticastDelegate
            => Statement(ExpressionBuilder.Fragment(lambda, arguments));

        /// <summary>
        /// Adds instance method call statement.
        /// </summary>
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="method">The method to be called.</param>
        /// <param name="arguments">Method call arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Call(Expression instance, MethodInfo method, IEnumerable<Expression> arguments)
            => Statement(Expression.Call(instance, method, arguments));

        /// <summary>
        /// Adds instance method call statement which is not invoked if <paramref name="instance"/> is <see langword="null"/>.
        /// </summary>
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="method">The method to be called.</param>
        /// <param name="arguments">Method call arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void NullSafeCall(Expression instance, MethodInfo method, IEnumerable<Expression> arguments)
            => Statement(instance.IfNotNull(target => Expression.Call(target, method, arguments)));

        /// <summary>
        /// Adds instance method call statement.
        /// </summary>
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="method">The method to be called.</param>
        /// <param name="arguments">Method call arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Call(Expression instance, MethodInfo method, params Expression[] arguments)
            => Call(instance, method, (IEnumerable<Expression>)arguments);

        /// <summary>
        /// Adds instance method call statement which is not invoked if <paramref name="instance"/> is <see langword="null"/>.
        /// </summary>
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="method">The method to be called.</param>
        /// <param name="arguments">Method call arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void NullSafeCall(Expression instance, MethodInfo method, params Expression[] arguments)
            => NullSafeCall(instance, method, (IEnumerable<Expression>)arguments);

        /// <summary>
        /// Adds instance method call statement.
        /// </summary>
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="methodName">The method to be called.</param>
        /// <param name="arguments">Method call arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Call(Expression instance, string methodName, params Expression[] arguments)
            => Statement(instance.Call(methodName, arguments));

        /// <summary>
        /// Adds instance method call statement which is not invoked if <paramref name="instance"/> is <see langword="null"/>.
        /// </summary>
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="methodName">The method to be called.</param>
        /// <param name="arguments">Method call arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void NullSafeCall(Expression instance, string methodName, params Expression[] arguments)
            => Statement(instance.IfNotNull(target => target.Call(methodName, arguments)));

        /// <summary>
        /// Adds static method call statement.
        /// </summary>
        /// <param name="method">The method to be called.</param>
        /// <param name="arguments">Method call arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void CallStatic(MethodInfo method, IEnumerable<Expression> arguments)
            => Statement(Expression.Call(null, method, arguments));

        /// <summary>
        /// Adds static method call statement.
        /// </summary>
        /// <param name="method">The method to be called.</param>
        /// <param name="arguments">Method call arguments.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void CallStatic(MethodInfo method, params Expression[] arguments)
            => CallStatic(method, (IEnumerable<Expression>)arguments);

        /// <summary>
        /// Adds static method call.
        /// </summary>
        /// <param name="type">The type that declares static method.</param>
        /// <param name="methodName">The name of the static method.</param>
        /// <param name="arguments">The arguments to be passed into static method.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void CallStatic(Type type, string methodName, params Expression[] arguments)
            => Statement(type.CallStatic(methodName, arguments));

        /// <summary>
        /// Constructs static method call.
        /// </summary>
        /// <typeparam name="T">The type that declares static method.</typeparam>
        /// <param name="methodName">The name of the static method.</param>
        /// <param name="arguments">The arguments to be passed into static method.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void CallStatic<T>(string methodName, params Expression[] arguments)
            => CallStatic(typeof(T), methodName, arguments);

        /// <summary>
        /// Declares label of the specified type.
        /// </summary>
        /// <param name="type">The type of landing site.</param>
        /// <param name="name">The optional name of the label.</param>
        /// <returns>Declared label.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static LabelTarget Label(Type type, string? name = null)
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
        public static LabelTarget Label<T>(string? name = null) => Label(typeof(T), name);

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
        public static void Label(LabelTarget target) => Statement(Expression.Label(target));

        private static void Goto(LabelTarget target, Expression? value, GotoExpressionKind kind)
            => Statement(Expression.MakeGoto(kind, target, value, value?.Type ?? typeof(void)));

        /// <summary>
        /// Adds unconditional control transfer statement to this scope.
        /// </summary>
        /// <param name="target">The label reference.</param>
        /// <param name="value">The value to be associated with the control transfer.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Goto(LabelTarget target, Expression? value) => Goto(target, value, GotoExpressionKind.Goto);

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
            LexicalScope.Current.DeclareVariable(variable);
            return variable;
        }

        /// <summary>
        /// Declares initialized local variable of automatically
        /// inferred type.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>var i = expr;</c>.
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
        /// <param name="configureAwait"><see langword="true"/> to call <see cref="System.Threading.Tasks.Task.ConfigureAwait(bool)"/> with <see langword="false"/> argument.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Await(Expression asyncResult, bool configureAwait = false) => Statement(asyncResult.Await(configureAwait));

        /// <summary>
        /// Adds if-then-else statement to this scope.
        /// </summary>
        /// <param name="test">Test expression.</param>
        /// <returns>Conditional statement builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static ConditionalBuilder If(Expression test) => new(test, LexicalScope.Current);

        /// <summary>
        /// Constructs positive branch of the conditional expression.
        /// </summary>
        /// <param name="builder">Conditional statement builder.</param>
        /// <param name="body">Branch builder.</param>
        /// <returns>Conditional expression builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static ConditionalBuilder Then(this ConditionalBuilder builder, Action body)
        {
            using var statement = BranchStatement.Positive(builder);
            return statement.Build(body);
        }

        /// <summary>
        /// Constructs negative branch of the conditional expression.
        /// </summary>
        /// <param name="builder">Conditional statement builder.</param>
        /// <param name="body">Branch builder.</param>
        /// <returns>Conditional expression builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static ConditionalBuilder Else(this ConditionalBuilder builder, Action body)
        {
            using var statement = BranchStatement.Negative(builder);
            return statement.Build(body);
        }

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
        /// Adds <c>while</c> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/while">while Statement</seealso>
        public static void While(Expression test, Action<LoopContext> body)
        {
            using var statement = WhileStatement.While(test);
            statement.Place(body);
        }

        /// <summary>
        /// Adds <c>while</c> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/while">while Statement</seealso>
        public static void While(Expression test, Action body)
        {
            using var statement = WhileStatement.While(test);
            statement.Place(body);
        }

        /// <summary>
        /// Adds <c>do{ } while(condition);</c> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/do">do-while Statement</seealso>
        public static void DoWhile(Expression test, Action<LoopContext> body)
        {
            using var statement = WhileStatement.Until(test);
            statement.Place(body);
        }

        /// <summary>
        /// Adds <c>do{ } while(condition);</c> loop statement.
        /// </summary>
        /// <param name="test">Loop continuation condition.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/do">do-while Statement</seealso>
        public static void DoWhile(Expression test, Action body)
        {
            using var statement = WhileStatement.Until(test);
            statement.Place(body);
        }

        /// <summary>
        /// Adds <c>foreach</c> loop statement.
        /// </summary>
        /// <param name="collection">The expression providing enumerable collection.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/foreach-in">foreach Statement</seealso>
        public static void ForEach(Expression collection, Action<MemberExpression, LoopContext> body)
        {
            using var statement = new ForEachStatement(collection);
            statement.Place(body);
        }

        /// <summary>
        /// Adds <c>foreach</c> loop statement.
        /// </summary>
        /// <param name="collection">The expression providing enumerable collection.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/foreach-in">foreach Statement</seealso>
        public static void ForEach(Expression collection, Action<MemberExpression> body)
        {
            using var statement = new ForEachStatement(collection);
            statement.Place(body);
        }

        /// <summary>
        /// Adds <c>await foreach</c> loop statement.
        /// </summary>
        /// <param name="collection">The expression providing enumerable collection.</param>
        /// <param name="body">Loop body.</param>
        /// <param name="cancellationToken">The expression of type <see cref="CancellationToken"/>.</param>
        /// <param name="configureAwait"><see langword="true"/> to call <see cref="ValueTask.ConfigureAwait(bool)"/> with <see langword="false"/> argument when awaiting <see cref="IAsyncEnumerator{T}.MoveNextAsync"/> method.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-8#asynchronous-streams">Async Streams</seealso>
        public static void AwaitForEach(Expression collection, Action<MemberExpression, LoopContext> body, Expression? cancellationToken = null, bool configureAwait = false)
        {
            using var statement = new AwaitForEachStatement(collection, cancellationToken, configureAwait);
            statement.Place(body);
        }

        /// <summary>
        /// Adds <c>await foreach</c> loop statement.
        /// </summary>
        /// <param name="collection">The expression providing enumerable collection.</param>
        /// <param name="body">Loop body.</param>
        /// <param name="cancellationToken">The expression of type <see cref="CancellationToken"/>.</param>
        /// <param name="configureAwait"><see langword="true"/> to call <see cref="ValueTask.ConfigureAwait(bool)"/> with <see langword="false"/> argument when awaiting <see cref="IAsyncEnumerator{T}.MoveNextAsync"/> method.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-8#asynchronous-streams">Async Streams</seealso>
        public static void AwaitForEach(Expression collection, Action<MemberExpression> body, Expression? cancellationToken = null, bool configureAwait = false)
        {
            using var statement = new AwaitForEachStatement(collection, cancellationToken, configureAwait);
            statement.Place(body);
        }

        /// <summary>
        /// Adds <c>for</c> loop statement.
        /// </summary>
        /// <remarks>
        /// This builder constructs the statement equivalent to <c>for(var i = initializer; condition; iter){ body; }</c>.
        /// </remarks>
        /// <param name="initializer">Loop variable initialization expression.</param>
        /// <param name="condition">Loop continuation condition.</param>
        /// <param name="iteration">Iteration statements.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/for">for Statement</seealso>
        public static void For(Expression initializer, ForExpression.LoopBuilder.Condition condition, Action<ParameterExpression> iteration, Action<ParameterExpression, LoopContext> body)
        {
            using var statement = new ForStatement(initializer, condition, iteration);
            statement.Place(body);
        }

        /// <summary>
        /// Adds <c>for</c> loop statement.
        /// </summary>
        /// <remarks>
        /// This builder constructs the statement equivalent to <c>for(var i = initializer; condition; iter){ body; }</c>.
        /// </remarks>
        /// <param name="initializer">Loop variable initialization expression.</param>
        /// <param name="condition">Loop continuation condition.</param>
        /// <param name="iteration">Iteration statements.</param>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/for">for Statement</seealso>
        public static void For(Expression initializer, ForExpression.LoopBuilder.Condition condition, Action<ParameterExpression> iteration, Action<ParameterExpression> body)
        {
            using var statement = new ForStatement(initializer, condition, iteration);
            statement.Place(body);
        }

        /// <summary>
        /// Adds generic loop statement.
        /// </summary>
        /// <remarks>
        /// This loop is equivalent to <c>while(true){ }</c>.
        /// </remarks>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Loop(Action<LoopContext> body)
        {
            using var statement = new LoopStatement();
            statement.Place(body);
        }

        /// <summary>
        /// Adds generic loop statement.
        /// </summary>
        /// <param name="body">Loop body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Loop(Action body)
        {
            using var statement = new LoopStatement();
            statement.Place(body);
        }

        /// <summary>
        /// Adds <c>throw</c> statement to the compound statement.
        /// </summary>
        /// <param name="exception">The exception to be thrown.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Throw(Expression exception) => Statement(Expression.Throw(exception));

        /// <summary>
        /// Adds <c>throw</c> statement to the compound statement.
        /// </summary>
        /// <typeparam name="TException">The exception to be thrown.</typeparam>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static void Throw<TException>()
            where TException : Exception, new()
            => Throw(Expression.New(typeof(TException)));

        /// <summary>
        /// Adds re-throw statement.
        /// </summary>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of catch clause.</exception>
        public static void Rethrow()
        {
            if (LexicalScope.IsInScope<CatchStatement>())
                Statement(Expression.Rethrow());
            else
                throw new InvalidOperationException(ExceptionMessages.InvalidRethrow);
        }

        /// <summary>
        /// Adds <c>using</c> statement.
        /// </summary>
        /// <param name="resource">The expression representing disposable resource.</param>
        /// <param name="body">The body of the statement.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement">using Statement</seealso>
        public static void Using(Expression resource, Action<ParameterExpression> body)
        {
            using var statement = new UsingStatement(resource);
            statement.Place(body);
        }

        /// <summary>
        /// Adds <c>using</c> statement.
        /// </summary>
        /// <param name="resource">The expression representing disposable resource.</param>
        /// <param name="body">The body of the statement.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement">using Statement</seealso>
        public static void Using(Expression resource, Action body)
        {
            using var statement = new UsingStatement(resource);
            statement.Place(body);
        }

        /// <summary>
        /// Adds <c>await using</c> statement.
        /// </summary>
        /// <param name="resource">The expression representing disposable resource.</param>
        /// <param name="body">The body of the statement.</param>
        /// <param name="configureAwait"><see langword="true"/> to call <see cref="ValueTask.ConfigureAwait(bool)"/> with <see langword="false"/> argument when awaiting <see cref="IAsyncDisposable.DisposeAsync"/> method.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync#using-async-disposable">Using async disposable</seealso>
        public static void AwaitUsing(Expression resource, Action<ParameterExpression> body, bool configureAwait = false)
        {
            using var statement = new AwaitUsingStatement(resource, configureAwait);
            statement.Place(body);
        }

        /// <summary>
        /// Adds <c>await using</c> statement.
        /// </summary>
        /// <param name="resource">The expression representing disposable resource.</param>
        /// <param name="body">The body of the statement.</param>
        /// <param name="configureAwait"><see langword="true"/> to call <see cref="ValueTask.ConfigureAwait(bool)"/> with <see langword="false"/> argument when awaiting <see cref="IAsyncDisposable.DisposeAsync"/> method.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync#using-async-disposable">Using async disposable</seealso>
        public static void AwaitUsing(Expression resource, Action body, bool configureAwait = false)
        {
            using var statement = new AwaitUsingStatement(resource, configureAwait);
            statement.Place(body);
        }

        /// <summary>
        /// Adds <c>lock</c> statement.
        /// </summary>
        /// <param name="syncRoot">The object to be locked during execution of the compound statement.</param>
        /// <param name="body">Synchronized scope of code.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement">lock Statement</seealso>
        public static void Lock(Expression syncRoot, Action<ParameterExpression> body)
        {
            using var statement = new LockStatement(syncRoot);
            statement.Place(body);
        }

        /// <summary>
        /// Adds <c>lock</c> statement.
        /// </summary>
        /// <param name="syncRoot">The object to be locked during execution of the compound statement.</param>
        /// <param name="body">Synchronized scope of code.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement">lock Statement</seealso>
        public static void Lock(Expression syncRoot, Action body)
        {
            using var statement = new LockStatement(syncRoot);
            statement.Place(body);
        }

        /// <summary>
        /// Adds compound statement hat repeatedly refer to a single object or
        /// structure so that the statements can use a simplified syntax when accessing members
        /// of the object or structure.
        /// </summary>
        /// <param name="expression">The implicitly referenced object.</param>
        /// <param name="body">The statement body.</param>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/visual-basic/language-reference/statements/with-end-with-statement">With..End Statement</seealso>
        public static void With(Expression expression, Action<ParameterExpression> body)
        {
            using var statement = new WithStatement(expression);
            statement.Place(body);
        }

        /// <summary>
        /// Adds selection expression.
        /// </summary>
        /// <param name="value">The value to be handled by the selection expression.</param>
        /// <returns>A new instance of selection expression builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/switch">switch Statement</seealso>
        public static SwitchBuilder Switch(Expression value) => new(value, LexicalScope.Current);

        /// <summary>
        /// Adds pattern match statement.
        /// </summary>
        /// <param name="value">The value to be matched with patterns.</param>
        /// <returns>Pattern matcher.</returns>
        public static MatchBuilder Match(Expression value) => new(value, LexicalScope.Current);

        /// <summary>
        /// Defines pattern matching.
        /// </summary>
        /// <param name="builder">Pattern matching builder.</param>
        /// <param name="pattern">The condition representing pattern.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public static MatchBuilder Case(this MatchBuilder builder, MatchBuilder.Pattern pattern, Action<ParameterExpression> body)
        {
            using var statement = builder.Case(pattern);
            return statement.Build(body);
        }

        /// <summary>
        /// Defines pattern matching based on the expected type of value.
        /// </summary>
        /// <param name="builder">Pattern matching builder.</param>
        /// <param name="expectedType">The expected type of the value.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public static MatchBuilder Case(this MatchBuilder builder, Type expectedType, Action<ParameterExpression> body)
        {
            using var statement = builder.Case(expectedType);
            return statement.Build(body);
        }

        /// <summary>
        /// Defines pattern matching based on the expected type of value.
        /// </summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <param name="builder">Pattern matching builder.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public static MatchBuilder Case<T>(this MatchBuilder builder, Action<ParameterExpression> body)
            => Case(builder, typeof(T), body);

        /// <summary>
        /// Defines pattern matching based on the expected type of value.
        /// </summary>
        /// <param name="builder">Pattern matching builder.</param>
        /// <param name="expectedType">The expected type of the value.</param>
        /// <param name="pattern">Additional condition associated with the value.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public static MatchBuilder Case(this MatchBuilder builder, Type expectedType, MatchBuilder.Pattern pattern, Action<ParameterExpression> body)
        {
            using var statement = builder.Case(expectedType, pattern);
            return statement.Build(body);
        }

        /// <summary>
        /// Defines pattern matching based on the expected type of value.
        /// </summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <param name="builder">Pattern matching builder.</param>
        /// <param name="pattern">Additional condition associated with the value.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public static MatchBuilder Case<T>(this MatchBuilder builder, MatchBuilder.Pattern pattern, Action<ParameterExpression> body)
            => Case(builder, typeof(T), pattern, body);

        /// <summary>
        /// Defines pattern matching based on structural matching.
        /// </summary>
        /// <param name="builder">Pattern matching builder.</param>
        /// <param name="structPattern">The structure pattern represented by instance of anonymous type.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public static MatchBuilder Case(this MatchBuilder builder, object structPattern, Action<ParameterExpression> body)
        {
            using var statement = builder.Case(structPattern);
            return statement.Build(body);
        }

        /// <summary>
        /// Defines pattern matching based on structural matching.
        /// </summary>
        /// <param name="builder">Pattern matching builder.</param>
        /// <param name="memberName">The name of the field or property.</param>
        /// <param name="memberValue">The expected value of the field or property.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public static MatchBuilder Case(this MatchBuilder builder, string memberName, Expression memberValue, Action<MemberExpression> body)
        {
            using var statement = builder.Case(memberName, memberValue);
            return statement.Build(body);
        }

        /// <summary>
        /// Defines pattern matching based on structural matching.
        /// </summary>
        /// <param name="builder">Pattern matching builder.</param>
        /// <param name="memberName1">The name of the first field or property.</param>
        /// <param name="memberValue1">The expected value of the first field or property.</param>
        /// <param name="memberName2">The name of the second field or property.</param>
        /// <param name="memberValue2">The expected value of the second field or property.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public static MatchBuilder Case(this MatchBuilder builder, string memberName1, Expression memberValue1, string memberName2, Expression memberValue2, Action<MemberExpression, MemberExpression> body)
        {
            using var statement = builder.Case(memberName1, memberValue1, memberName2, memberValue2);
            return statement.Build(body);
        }

        /// <summary>
        /// Defines pattern matching based on structural matching.
        /// </summary>
        /// <param name="builder">Pattern matching builder.</param>
        /// <param name="memberName1">The name of the first field or property.</param>
        /// <param name="memberValue1">The expected value of the first field or property.</param>
        /// <param name="memberName2">The name of the second field or property.</param>
        /// <param name="memberValue2">The expected value of the second field or property.</param>
        /// <param name="memberName3">The name of the third field or property.</param>
        /// <param name="memberValue3">The expected value of the third field or property.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public static MatchBuilder Case(this MatchBuilder builder, string memberName1, Expression memberValue1, string memberName2, Expression memberValue2, string memberName3, Expression memberValue3, Action<MemberExpression, MemberExpression, MemberExpression> body)
        {
            using var statement = builder.Case(memberName1, memberValue1, memberName2, memberValue2, memberName3, memberValue3);
            return statement.Build(body);
        }

        /// <summary>
        /// Defines default behavior in case when all defined patterns are false positive.
        /// </summary>
        /// <param name="builder">Pattern matching builder.</param>
        /// <param name="body">The body to be executed as default case.</param>
        /// <returns><c>this</c> builder.</returns>
        public static MatchBuilder Default(this MatchBuilder builder, Action<ParameterExpression> body)
        {
            using var statement = builder.Default();
            return statement.Build(body);
        }

        /// <summary>
        /// Specifies a pattern to compare to the match expression
        /// and action to be executed if matching is successful.
        /// </summary>
        /// <param name="builder">Selection builder.</param>
        /// <param name="testValues">A list of test values.</param>
        /// <param name="body">The block code to be executed if input value is equal to one of test values.</param>
        /// <returns>Modified selection builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static SwitchBuilder Case(this SwitchBuilder builder, IEnumerable<Expression> testValues, Action body)
        {
            using var statement = builder.Case(testValues);
            return statement.Build(body);
        }

        /// <summary>
        /// Specifies a pattern to compare to the match expression
        /// and action to be executed if matching is successful.
        /// </summary>
        /// <param name="builder">Selection builder.</param>
        /// <param name="test">Single test value.</param>
        /// <param name="body">The block code to be executed if input value is equal to one of test values.</param>
        /// <returns>Modified selection builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static SwitchBuilder Case(this SwitchBuilder builder, Expression test, Action body)
            => Case(builder, Seq.Singleton(test), body);

        /// <summary>
        /// Specifies the switch section to execute if the match expression
        /// doesn't match any other cases.
        /// </summary>
        /// <param name="builder">Selection builder.</param>
        /// <param name="body">The block code to be executed if input value is equal to one of test values.</param>
        /// <returns>Modified selection builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static SwitchBuilder Default(this SwitchBuilder builder, Action body)
        {
            using var statement = builder.Default();
            return statement.Build(body);
        }

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <param name="builder">Structured exception handling builder.</param>
        /// <param name="exceptionType">Expected exception.</param>
        /// <param name="filter">Additional filter to be applied to the caught exception.</param>
        /// <param name="handler">Exception handling block.</param>
        /// <returns>Structured exception handler builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static TryBuilder Catch(this TryBuilder builder, Type exceptionType, TryBuilder.Filter? filter, Action<ParameterExpression> handler)
        {
            using var statement = new CatchStatement(builder, exceptionType, filter);
            return statement.Build(handler);
        }

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <param name="builder">Structured exception handling builder.</param>
        /// <param name="exceptionType">Expected exception.</param>
        /// <param name="handler">Exception handling block.</param>
        /// <returns>Structured exception handler.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static TryBuilder Catch(this TryBuilder builder, Type exceptionType, Action handler)
        {
            using var statement = new CatchStatement(builder, exceptionType);
            return statement.Build(handler);
        }

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <param name="builder">Structured exception handling builder.</param>
        /// <param name="exceptionType">Expected exception.</param>
        /// <param name="handler">Exception handling block.</param>
        /// <returns>Structured exception handler.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static TryBuilder Catch(this TryBuilder builder, Type exceptionType, Action<ParameterExpression> handler)
            => Catch(builder, exceptionType, null, handler);

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <typeparam name="TException">Expected exception.</typeparam>
        /// <param name="builder">Structured exception handling builder.</param>
        /// <param name="handler">Exception handling block.</param>
        /// <returns>Structured exception handler.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static TryBuilder Catch<TException>(this TryBuilder builder, Action<ParameterExpression> handler)
            where TException : Exception
            => Catch(builder, typeof(TException), handler);

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <typeparam name="TException">Expected exception.</typeparam>
        /// <param name="builder">Structured exception handling builder.</param>
        /// <param name="handler">Exception handling block.</param>
        /// <returns>Structured exception handler.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static TryBuilder Catch<TException>(this TryBuilder builder, Action handler)
            where TException : Exception
            => Catch(builder, typeof(TException), handler);

        /// <summary>
        /// Constructs exception handling section that may capture any exception.
        /// </summary>
        /// <param name="builder">Structured exception handling builder.</param>
        /// <param name="handler">Exception handling block.</param>
        /// <returns>Structured exception handler.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static TryBuilder Catch(this TryBuilder builder, Action handler)
        {
            using var statement = new CatchStatement(builder);
            return statement.Build(handler);
        }

        /// <summary>
        /// Constructs block of code which will be executed in case
        /// of any exception.
        /// </summary>
        /// <param name="builder">Structured exception handling builder.</param>
        /// <param name="fault">Fault handling block.</param>
        /// <returns><c>this</c> builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static TryBuilder Fault(this TryBuilder builder, Action fault)
        {
            using var statement = new FaultStatement(builder);
            return statement.Build(fault);
        }

        /// <summary>
        /// Adds structured exception handling statement.
        /// </summary>
        /// <param name="scope"><c>try</c> block builder.</param>
        /// <returns>Structured exception handling builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/try-catch-finally">try-catch-finally Statement</seealso>
        public static TryBuilder Try(Action scope)
        {
            using var statement = new TryStatement();
            return statement.Build(scope);
        }

        /// <summary>
        /// Adds structured exception handling statement.
        /// </summary>
        /// <param name="body"><c>try</c> block.</param>
        /// <returns>Structured exception handling builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/try-catch-finally">try-catch-finally Statement</seealso>
        public static TryBuilder Try(Expression body) => new(body, LexicalScope.Current);

        /// <summary>
        /// Constructs block of code run when control leaves a <c>try</c> statement.
        /// </summary>
        /// <param name="builder">Structured exception handling builder.</param>
        /// <param name="body">The block of code to be executed.</param>
        /// <returns><c>this</c> builder.</returns>
        /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
        public static TryBuilder Finally(this TryBuilder builder, Action body)
        {
            using var statement = new FinallyStatement(builder);
            return statement.Build(body);
        }

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
            var loop = LexicalScope.FindScope<LoopLexicalScope>() ?? throw new InvalidOperationException(ExceptionMessages.LoopNotAvailable);
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
            var loop = LexicalScope.FindScope<LoopLexicalScope>() ?? throw new InvalidOperationException(ExceptionMessages.LoopNotAvailable);
            Break(new LoopContext(loop));
        }

        /// <summary>
        /// Adds <c>return</c> instruction to return from
        /// underlying lambda function having non-<see cref="void"/>
        /// return type.
        /// </summary>
        /// <param name="result">Optional value to be returned from the lambda function.</param>
        /// <exception cref="InvalidOperationException">This method is not called from within body of lambda function.</exception>
        public static void Return(Expression? result = null)
        {
            var lambda = LexicalScope.FindScope<LambdaExpression>() ?? throw new InvalidOperationException(ExceptionMessages.OutOfLexicalScope);
            Statement(lambda.Return(result));
        }

        /// <summary>
        /// Constructs multi-line lambda function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate describing signature of lambda function.</typeparam>
        /// <param name="tailCall"><see langword="true"/> if the lambda expression will be compiled with the tail call optimization, otherwise <see langword="false"/>.</param>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        public static Expression<TDelegate> Lambda<TDelegate>(bool tailCall, Action<LambdaContext> body)
            where TDelegate : Delegate
        {
            using var expression = new LambdaExpression<TDelegate>(tailCall);
            return expression.Build(body);
        }

        /// <summary>
        /// Constructs single-line lambda function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate describing signature of lambda function.</typeparam>
        /// <param name="tailCall"><see langword="true"/> if the lambda expression will be compiled with the tail call optimization, otherwise <see langword="false"/>.</param>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        public static Expression<TDelegate> Lambda<TDelegate>(bool tailCall, Func<LambdaContext, Expression> body)
            where TDelegate : Delegate
        {
            using var expression = new LambdaExpression<TDelegate>(tailCall);
            return expression.Build(body);
        }

        /// <summary>
        /// Constructs single-line lambda function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate describing signature of lambda function.</typeparam>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        public static Expression<TDelegate> Lambda<TDelegate>(Func<LambdaContext, Expression> body)
            where TDelegate : Delegate
            => Lambda<TDelegate>(false, body);

        /// <summary>
        /// Constructs multi-line lambda function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate describing signature of lambda function.</typeparam>
        /// <param name="tailCall"><see langword="true"/> if the lambda expression will be compiled with the tail call optimization, otherwise <see langword="false"/>.</param>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        public static Expression<TDelegate> Lambda<TDelegate>(bool tailCall, Action<LambdaContext, ParameterExpression> body)
            where TDelegate : Delegate
        {
            using var expression = new LambdaExpression<TDelegate>(tailCall);
            return expression.Build(body);
        }

        /// <summary>
        /// Constructs multi-line lambda function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate describing signature of lambda function.</typeparam>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        public static Expression<TDelegate> Lambda<TDelegate>(Action<LambdaContext> body)
            where TDelegate : Delegate
            => Lambda<TDelegate>(false, body);

        /// <summary>
        /// Constructs multi-line lambda function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate describing signature of lambda function.</typeparam>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        public static Expression<TDelegate> Lambda<TDelegate>(Action<LambdaContext, ParameterExpression> body)
            where TDelegate : Delegate
            => Lambda<TDelegate>(false, body);

        /// <summary>
        /// Constructs multi-line async lambda function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate describing signature of lambda function.</typeparam>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        /// <seealso cref="AwaitExpression"/>
        /// <seealso cref="AsyncResultExpression"/>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/#BKMK_HowtoWriteanAsyncMethod">Async methods</seealso>
        public static Expression<TDelegate> AsyncLambda<TDelegate>(Action<LambdaContext> body)
            where TDelegate : Delegate
        {
            using var statement = new AsyncLambdaExpression<TDelegate>();
            return statement.Build(body);
        }

        /// <summary>
        /// Constructs multi-line async lambda function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate describing signature of lambda function.</typeparam>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        /// <seealso cref="AwaitExpression"/>
        /// <seealso cref="AsyncResultExpression"/>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/#BKMK_HowtoWriteanAsyncMethod">Async methods</seealso>
        public static Expression<TDelegate> AsyncLambda<TDelegate>(Action<LambdaContext, ParameterExpression> body)
            where TDelegate : Delegate
        {
            using var statement = new AsyncLambdaExpression<TDelegate>();
            return statement.Build(body);
        }

        private static LambdaExpressionTree AsyncLambda<TScope>(Type[] parameters, Type returnType, bool isValueTask, TScope scope)
            where TScope : MulticastDelegate
        {
            var args = parameters.Concat(new Type[] { new TaskType(returnType, isValueTask) }, parameters.LongLength);
            var type = LambdaExpressionTree.GetDelegateType(args);
            type = typeof(AsyncLambdaExpression<>).MakeGenericType(type);
            using var expression = (LambdaExpression?)Activator.CreateInstance(type);
            Debug.Assert(expression is ILexicalScope<LambdaExpressionTree, TScope>);
            return ((ILexicalScope<LambdaExpressionTree, TScope>)expression).Build(scope);
        }

        /// <summary>
        /// Constructs multi-line async lambda function capturing the current lexical scope.
        /// </summary>
        /// <param name="parameters">The parameters.</param>
        /// <param name="returnType">The return type. Pass <c>typeof(void)</c> for void return type.</param>
        /// <param name="isValueTask"><see langword="true"/> to use <see cref="ValueTask"/> as an actual return type; <see langword="false"/> to use <see cref="Task"/>.</param>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        /// <seealso cref="AwaitExpression"/>
        /// <seealso cref="AsyncResultExpression"/>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/#BKMK_HowtoWriteanAsyncMethod">Async methods</seealso>
        public static LambdaExpressionTree AsyncLambda(Type[] parameters, Type returnType, bool isValueTask, Action<LambdaContext> body)
            => AsyncLambda<Action<LambdaContext>>(parameters, returnType, isValueTask, body);

        /// <summary>
        /// Constructs multi-line async lambda function capturing the current lexical scope.
        /// </summary>
        /// <param name="parameters">The parameters.</param>
        /// <param name="returnType">The return type. Pass <c>typeof(void)</c> for void return type.</param>
        /// <param name="isValueTask"><see langword="true"/> to use <see cref="ValueTask"/> as an actual return type; <see langword="false"/> to use <see cref="Task"/>.</param>
        /// <param name="body">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        /// <seealso cref="AwaitExpression"/>
        /// <seealso cref="AsyncResultExpression"/>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/#BKMK_HowtoWriteanAsyncMethod">Async methods</seealso>
        public static LambdaExpressionTree AsyncLambda(Type[] parameters, Type returnType, bool isValueTask, Action<LambdaContext, ParameterExpression> body)
            => AsyncLambda<Action<LambdaContext, ParameterExpression>>(parameters, returnType, isValueTask, body);

        /// <summary>
        /// Adds free-form expression as a statement to the current lexical scope.
        /// </summary>
        /// <param name="expr">The expression to add.</param>
        public static void Statement(Expression expr)
            => LexicalScope.Current.AddStatement(expr);
    }
}