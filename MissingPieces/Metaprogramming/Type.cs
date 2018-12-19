using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace MissingPieces.Metaprogramming
{
	using static Reflection.Methods;

	/// <summary>
	/// Provides typed access to class or value type metadata.
	/// </summary>
	public static class Type<T>
	{
		/// <summary>
		/// Gets reflected type.
		/// </summary>
		public static Type RuntimeType => typeof(T);

		/// <summary>
		/// Provides constructor definition based on delegate signature.
		/// </summary>
		/// <typeparam name="D">Type of delegate representing constructor of type <typeparamref name="D"/>.</typeparam>
		public sealed class Constructor<D>: ConstructorInfo, IConstructor<D>, IEquatable<ConstructorInfo>, IEquatable<Constructor<D>>
			where D : class, MulticastDelegate
		{
			private readonly D invoker;
			private readonly ConstructorInfo ctor;

			private Constructor(ConstructorInfo ctor)
			{
				this.ctor = ctor;
				if (ctor is null)
					invoker = Lambda<D>(Default<T>.Expression).Compile();
				else
				{
					var parameters = ctor.GetParameters().Map(p => Parameter(p.ParameterType));
					invoker = Lambda<D>(New(ctor, parameters), parameters).Compile();
				}
			}

			public static implicit operator D(Constructor<D> ctor) => ctor?.invoker;

			public override string Name => ctor?.Name ?? ".ctor";

			ConstructorInfo IMember<ConstructorInfo>.RuntimeMember => ctor;

			D IMethod<ConstructorInfo, D>.Invoker => invoker;

			public override MethodAttributes Attributes => ctor == null ? invoker.Method.Attributes : ctor.Attributes;

			public override RuntimeMethodHandle MethodHandle => ctor == null ? invoker.Method.MethodHandle : ctor.MethodHandle;

			public override Type DeclaringType => ctor?.DeclaringType ?? RuntimeType;

			public override Type ReflectedType => ctor?.ReflectedType ?? invoker.Method.ReflectedType;

			public override CallingConventions CallingConvention => ctor == null ? invoker.Method.CallingConvention : ctor.CallingConvention;

			public override bool ContainsGenericParameters => false;

			public override IEnumerable<CustomAttributeData> CustomAttributes => ctor?.CustomAttributes ?? invoker.Method.CustomAttributes;

			public override MethodBody GetMethodBody() => ctor?.GetMethodBody() ?? invoker.Method.GetMethodBody();

			public override IList<CustomAttributeData> GetCustomAttributesData() => ctor?.GetCustomAttributesData() ?? invoker.Method.GetCustomAttributesData();

			public override Type[] GetGenericArguments() => Array.Empty<Type>();

			public override bool IsGenericMethod => false;

			public override bool IsGenericMethodDefinition => false;

			public override bool IsSecurityCritical => ctor == null ? invoker.Method.IsSecurityCritical : ctor.IsSecurityCritical;

			public override bool IsSecuritySafeCritical => ctor == null ? invoker.Method.IsSecuritySafeCritical : ctor.IsSecuritySafeCritical;

			public override bool IsSecurityTransparent => ctor == null ? invoker.Method.IsSecurityTransparent : ctor.IsSecurityTransparent;

			public override MemberTypes MemberType => MemberTypes.Constructor;

			public override int MetadataToken => ctor == null ? invoker.Method.MetadataToken : ctor.MetadataToken;

			public override MethodImplAttributes MethodImplementationFlags => ctor == null ? invoker.Method.MethodImplementationFlags : ctor.MethodImplementationFlags;

			public override Module Module => ctor?.Module ?? RuntimeType.Module;

			public override object Invoke(BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
				=> Invoke(null, invokeAttr, binder, parameters, culture);

			public override MethodImplAttributes GetMethodImplementationFlags() => MethodImplementationFlags;

			public override ParameterInfo[] GetParameters() => ctor?.GetParameters() ?? invoker.Method.GetParameters();

			public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
				=> ctor == null ? invoker.Method.Invoke(obj, invokeAttr, binder, parameters, culture) : ctor.Invoke(obj, invokeAttr, binder, parameters, culture);

			public override object[] GetCustomAttributes(bool inherit) 
				=> ctor?.GetCustomAttributes(inherit) ?? invoker.Method.GetCustomAttributes(inherit);

			public override object[] GetCustomAttributes(Type attributeType, bool inherit) 
				=> ctor?.GetCustomAttributes(attributeType, inherit) ?? invoker.Method.GetCustomAttributes(attributeType, inherit);

			public override bool IsDefined(Type attributeType, bool inherit)
				=> ctor == null ? invoker.Method.IsDefined(attributeType, inherit) : ctor.IsDefined(attributeType, inherit);

			public bool Equals(ConstructorInfo other) => ctor == other;

			public bool Equals(Constructor<D> other) => Equals(other?.ctor);

			public override bool Equals(object other)
			{
				switch(other)
				{
					case Constructor<D> ctor:
						return Equals(ctor);
					case ConstructorInfo ctor:
						return Equals(ctor);
					default:
						return false;
				}
			}

			public override string ToString() => ctor == null ? invoker.Method.ToString() : ctor.ToString();

			public override int GetHashCode() => ctor == null ? invoker.Method.GetHashCode() : ctor.GetHashCode();

			private static Constructor<D> Create(bool nonPublic)
			{
				var invokeMethod = Delegates.GetInvokeMethod<D>();

				if (RuntimeType.IsValueType && invokeMethod.GetParameters().LongLength == 0L)
					return new Constructor<D>(null);
				else
				{
					var flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | (nonPublic ? BindingFlags.NonPublic : BindingFlags.Public);
					var ctor = RuntimeType.GetConstructor(flags, Type.DefaultBinder, invokeMethod.GetParameterTypes(), Array.Empty<ParameterModifier>());
					return ctor is null || !invokeMethod.ReturnType.IsAssignableFrom(RuntimeType) ?
						null :
						new Constructor<D>(ctor);
				}
			}

			private static class Public
			{
				internal static readonly Constructor<D> Value = Create(false);
			}

			private static class NonPublic
			{
				internal static readonly Constructor<D> Value = Create(true);
			}

			/// <summary>
			/// Gets constructor matching to signature of delegate <typeparamref name="D"/>.
			/// </summary>
			/// <param name="nonPublic">True to reflect non-public constructor.</param>
			/// <returns>Reflected constructor; or null, if constructor doesn't exist.</returns>
			public static Constructor<D> GetOrNull(bool nonPublic = false) => nonPublic ? NonPublic.Value : Public.Value;

			/// <summary>
			/// Gets constructor matching to signature of delegate <typeparamref name="D"/>.
			/// </summary>
			/// <param name="nonPublic">True to reflect non-public constructor.</param>
			/// <typeparam name="E">Type of exception to throw if constructor doesn't exist.</typeparam>
			/// <returns>Reflected constructor.</returns>
			public static Constructor<D> GetOrThrow<E>(bool nonPublic = false)
				where E : Exception, new()
				=> GetOrNull(nonPublic) ?? throw new E();

			/// <summary>
			/// Gets constructor matching to signature of delegate <typeparamref name="D"/>.
			/// </summary>
			/// <param name="exceptionFactory">A factory used to produce exception.</param>
			/// <param name="nonPublic">True to reflect non-public constructor.</param>
			/// <typeparam name="E">Type of exception to throw if constructor doesn't exist.</typeparam>
			/// <returns>Reflected constructor.</returns>
			public static Constructor<D> GetOrThrow<E>(Func<E> exceptionFactory, bool nonPublic = false)
				where E : Exception
				=> GetOrNull(nonPublic) ?? throw exceptionFactory();
		}

		/// <summary>
		/// Provides typed access to constructor of type <typeparamref name="T"/>.
		/// </summary>
		public static class Constructor
		{
			/// <summary>
			/// Returns public constructor of type <typeparamref name="T"/> without parameters.
			/// </summary>
			/// <param name="nonPublic">True to reflect non-public constructor.</param>
			/// <returns>A delegate representing public constructor without parameters.</returns>
			/// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
			public static Constructor<Func<T>> Get(bool nonPublic = false)
				=> Constructor<Func<T>>.GetOrThrow(MissingConstructorException.Create<T>, nonPublic);

			/// <summary>
			/// Returns public constructor <typeparamref name="T"/> with single parameter of type <typeparamref name="P"/>.
			/// </summary>
			/// <param name="nonPublic">True to reflect non-public constructor.</param>
			/// <typeparam name="P">Type of constructor parameter.</typeparam>
			/// <returns>A delegate representing public constructor with single parameter.</returns>
			/// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
			public static Constructor<Func<P, T>> Get<P>(bool nonPublic = false)
				=> Constructor<Func<P, T>>.GetOrThrow(MissingConstructorException.Create<T, P>, nonPublic);

			/// <summary>
			/// Returns public constructor <typeparamref name="T"/> with two 
			/// parameters of type <typeparamref name="P1"/> and <typeparamref name="P2"/>.
			/// </summary>
			/// <param name="nonPublic">True to reflect non-public constructor.</param>
			/// <typeparam name="P1">Type of first constructor parameter.</typeparam>
			/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
			/// <returns>A delegate representing public constructor with two parameters.</returns>
			/// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
			public static Constructor<Func<P1, P2, T>> Get<P1, P2>(bool nonPublic = false)
				=> Constructor<Func<P1, P2, T>>.GetOrThrow(MissingConstructorException.Create<T, P1, P2>, nonPublic);

			/// <summary>
			/// Returns public constructor <typeparamref name="T"/> with three 
			/// parameters of type <typeparamref name="P1"/>, <typeparamref name="P2"/> and <typeparamref name="P3"/>.
			/// </summary>
			/// <param name="nonPublic">True to reflect non-public constructor.</param>
			/// <typeparam name="P1">Type of first constructor parameter.</typeparam>
			/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
			/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
			/// <returns>A delegate representing public constructor with three parameters.</returns>
			/// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
			public static Constructor<Func<P1, P2, P3, T>> Get<P1, P2, P3>(bool nonPublic = false)
				=> Constructor<Func<P1, P2, P3, T>>.GetOrThrow(MissingConstructorException.Create<T, P1, P2, P3>, nonPublic);

			/// <summary>
			/// Returns public constructor <typeparamref name="T"/> with four 
			/// parameters of type <typeparamref name="P1"/>, <typeparamref name="P2"/>, <typeparamref name="P3"/> and <typeparamref name="P4"/>.
			/// </summary>
			/// <param name="nonPublic">True to reflect non-public constructor.</param>
			/// <typeparam name="P1">Type of first constructor parameter.</typeparam>
			/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
			/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
			/// <typeparam name="P4">Type of fourth constructor parameter,</typeparam>
			/// <returns>A delegate representing public constructor with four parameters.</returns>
			/// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
			public static Constructor<Func<P1, P2, P3, P4, T>> Get<P1, P2, P3, P4>(bool nonPublic = false)
				=> Constructor<Func<P1, P2, P3, P4, T>>.GetOrThrow(MissingConstructorException.Create<T, P1, P2, P3, P4>, nonPublic);

			/// <summary>
			/// Returns public constructor <typeparamref name="T"/> with five 
			/// parameters of type <typeparamref name="P1"/>, <typeparamref name="P2"/>, 
			/// <typeparamref name="P3"/>, <typeparamref name="P4"/> and <typeparamref name="P5"/>.
			/// </summary>
			/// <param name="nonPublic">True to reflect non-public constructor.</param>
			/// <typeparam name="P1">Type of first constructor parameter.</typeparam>
			/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
			/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
			/// <typeparam name="P4">Type of fourth constructor parameter,</typeparam>
			/// <typeparam name="P5">Type of fifth constructor parameter.</typeparam>
			/// <returns>A delegate representing public constructor with five parameters.</returns>
			/// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
			public static Constructor<Func<P1, P2, P3, P4, P5, T>> Get<P1, P2, P3, P4, P5>(bool nonPublic = false)
				=> Constructor<Func<P1, P2, P3, P4, P5, T>>.GetOrThrow(MissingConstructorException.Create<T, P1, P2, P3, P4, P5>, nonPublic);

			/// <summary>
			/// Returns public constructor <typeparamref name="T"/> with six parameters.
			/// </summary>
			/// <param name="nonPublic">True to reflect non-public constructor.</param>
			/// <typeparam name="P1">Type of first constructor parameter.</typeparam>
			/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
			/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
			/// <typeparam name="P4">Type of fourth constructor parameter,</typeparam>
			/// <typeparam name="P5">Type of fifth constructor parameter.</typeparam>
			/// <typeparam name="P6">Type of sixth constructor parameter.</typeparam>
			/// <returns>A delegate representing public constructor with six parameters.</returns>
			/// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
			public static Constructor<Func<P1, P2, P3, P4, P5, P6, T>> Get<P1, P2, P3, P4, P5, P6>(bool nonPublic = false)
				=> Constructor<Func<P1, P2, P3, P4, P5, P6, T>>.GetOrThrow(MissingConstructorException.Create<T, P1, P2, P3, P4, P5, P6>, nonPublic);

			/// <summary>
			/// Returns public constructor <typeparamref name="T"/> with seven parameters.
			/// </summary>
			/// <param name="nonPublic">True to reflect non-public constructor.</param>
			/// <typeparam name="P1">Type of first constructor parameter.</typeparam>
			/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
			/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
			/// <typeparam name="P4">Type of fourth constructor parameter,</typeparam>
			/// <typeparam name="P5">Type of fifth constructor parameter.</typeparam>
			/// <typeparam name="P6">Type of sixth constructor parameter.</typeparam>
			/// <typeparam name="P7">Type of seventh constructor parameter.</typeparam>
			/// <returns>A delegate representing public constructor with seven parameters.</returns>
			/// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
			public static Constructor<Func<P1, P2, P3, P4, P5, P6, P7, T>> Get<P1, P2, P3, P4, P5, P6, P7>(bool nonPublic = false)
				=> Constructor<Func<P1, P2, P3, P4, P5, P6, P7, T>>.GetOrThrow(MissingConstructorException.Create<T, P1, P2, P3, P4, P5, P6, P7>, nonPublic);

			/// <summary>
			/// Returns public constructor <typeparamref name="T"/> with eight parameters.
			/// </summary>
			/// <param name="nonPublic">True to reflect non-public constructor.</param>
			/// <typeparam name="P1">Type of first constructor parameter.</typeparam>
			/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
			/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
			/// <typeparam name="P4">Type of fourth constructor parameter,</typeparam>
			/// <typeparam name="P5">Type of fifth constructor parameter.</typeparam>
			/// <typeparam name="P6">Type of sixth constructor parameter.</typeparam>
			/// <typeparam name="P7">Type of seventh constructor parameter.</typeparam>
			/// <typeparam name="P8">Type of eighth constructor parameter.</typeparam>
			/// <returns>A delegate representing public constructor with eight parameters.</returns>
			/// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
			public static Constructor<Func<P1, P2, P3, P4, P5, P6, P7, P8, T>> Get<P1, P2, P3, P4, P5, P6, P7, P8>(bool nonPublic = false)
				=> Constructor<Func<P1, P2, P3, P4, P5, P6, P7, P8, T>>.GetOrThrow(MissingConstructorException.Create<T, P1, P2, P3, P4, P5, P6, P7, P8>, nonPublic);

			/// <summary>
			/// Returns public constructor <typeparamref name="T"/> with nine parameters.
			/// </summary>
			/// <param name="nonPublic">True to reflect non-public constructor.</param>
			/// <typeparam name="P1">Type of first constructor parameter.</typeparam>
			/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
			/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
			/// <typeparam name="P4">Type of fourth constructor parameter,</typeparam>
			/// <typeparam name="P5">Type of fifth constructor parameter.</typeparam>
			/// <typeparam name="P6">Type of sixth constructor parameter.</typeparam>
			/// <typeparam name="P7">Type of seventh constructor parameter.</typeparam>
			/// <typeparam name="P8">Type of eighth constructor parameter.</typeparam>
			/// <typeparam name="P9">Type of ninth constructor parameter.</typeparam>
			/// <returns>A delegate representing public constructor with nine parameters.</returns>
			/// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
			public static Constructor<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, T>> Get<P1, P2, P3, P4, P5, P6, P7, P8, P9>(bool nonPublic = false)
				=> Constructor<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, T>>.GetOrThrow(MissingConstructorException.Create<T, P1, P2, P3, P4, P5, P6, P7, P8, P9>, nonPublic);

			/// <summary>
			/// Returns public constructor <typeparamref name="T"/> with ten parameters.
			/// </summary>
			/// <param name="nonPublic">True to reflect non-public constructor.</param>
			/// <typeparam name="P1">Type of first constructor parameter.</typeparam>
			/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
			/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
			/// <typeparam name="P4">Type of fourth constructor parameter,</typeparam>
			/// <typeparam name="P5">Type of fifth constructor parameter.</typeparam>
			/// <typeparam name="P6">Type of sixth constructor parameter.</typeparam>
			/// <typeparam name="P7">Type of seventh constructor parameter.</typeparam>
			/// <typeparam name="P8">Type of eighth constructor parameter.</typeparam>
			/// <typeparam name="P9">Type of ninth constructor parameter.</typeparam>
			/// <typeparam name="P9">Type of tenth constructor parameter.</typeparam>
			/// <returns>A delegate representing public constructor with ten parameters.</returns>
			/// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
			public static Constructor<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, T>> Get<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>(bool nonPublic = false)
				=> Constructor<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, T>>.GetOrThrow(MissingConstructorException.Create<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>, nonPublic);
		}

		/// <summary>
		/// Provides typed access to the property declared in type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="P">Type of property.</typeparam>
		public abstract class Property<P>: PropertyInfo, IProperty, IEquatable<Property<P>>, IEquatable<PropertyInfo>
		{
			private readonly PropertyInfo property;

			private protected Property(PropertyInfo property)
			{
				this.property = property;
			}

			public sealed override object GetValue(object obj, object[] index) => property.GetValue(obj, index);

			public sealed override void SetValue(object obj, object value, object[] index) => property.SetValue(obj, value, index);

			public sealed override string Name => property.Name;

			public sealed override bool CanRead => property.CanRead;

			public sealed override bool CanWrite => property.CanWrite;

			public sealed override MethodInfo GetMethod => property.GetMethod;

			public sealed override PropertyAttributes Attributes => property.Attributes;

			public sealed override Type PropertyType => property.PropertyType;

			public sealed override MethodInfo SetMethod => property.SetMethod;

			public sealed override MethodInfo[] GetAccessors(bool nonPublic) => property.GetAccessors(nonPublic);

			public sealed override object GetConstantValue() => property.GetConstantValue();

			public sealed override MethodInfo GetGetMethod(bool nonPublic) => property.GetGetMethod(nonPublic);

			public sealed override ParameterInfo[] GetIndexParameters() => property.GetIndexParameters();

			public sealed override Type[] GetOptionalCustomModifiers() => property.GetOptionalCustomModifiers();

			public sealed override object GetRawConstantValue() => property.GetRawConstantValue();

			public sealed override Type[] GetRequiredCustomModifiers() => property.GetRequiredCustomModifiers();

			public sealed override MethodInfo GetSetMethod(bool nonPublic) => property.GetSetMethod(nonPublic);

			public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
				=> property.GetValue(obj, invokeAttr, binder, index, culture);

			public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
				=> property.SetValue(obj, value, invokeAttr, binder, index, culture);

			public sealed override Type DeclaringType => property.DeclaringType;

			public sealed override MemberTypes MemberType => property.MemberType;

			public sealed override Type ReflectedType => property.ReflectedType;

			public sealed override object[] GetCustomAttributes(bool inherit) => property.GetCustomAttributes(inherit);
        	public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => property.GetCustomAttributes(attributeType, inherit);

			public sealed override bool IsDefined(Type attributeType, bool inherit) => property.IsDefined(attributeType, inherit);

			public sealed override int MetadataToken => property.MetadataToken;

			public sealed override Module Module => property.Module;

			public sealed override IList<CustomAttributeData> GetCustomAttributesData() => property.GetCustomAttributesData();

			public sealed override IEnumerable<CustomAttributeData> CustomAttributes => property.CustomAttributes;

			PropertyInfo IMember<PropertyInfo>.RuntimeMember => property;

			public bool Equals(PropertyInfo other) => property == other;

			public bool Equals(Property<P> other) 
				=>  other != null &&
					GetType() == other.GetType() &&
					property == other.property;

			public override bool Equals(object other)
			{
				switch(other)
				{
					case Property<P> property:
						return Equals(property);
					case PropertyInfo property:
						return Equals(property);
					default:
						return false;
				}
			}

			public override int GetHashCode() => property.GetHashCode();

			public static bool operator ==(Property<P> first, Property<P> second) => Equals(first, second);

			public static bool operator !=(Property<P> first, Property<P> second) => !Equals(first, second);

			public override string ToString() => property.ToString();


			/// <summary>
			/// Provides typed access to the static property.
			/// </summary>	
			public sealed class Static: Property<P>, IProperty<P>
			{
				private sealed class Cache: MemberCache<PropertyInfo, Static>
				{
					private readonly BindingFlags flags;

					internal Cache(BindingFlags flags) => this.flags = flags;

					private protected override Static CreateMember(string propertyName)
					{
						var property = RuntimeType.GetProperty(propertyName, flags);
						return property == null ? null : new Static(property, flags.HasFlag(BindingFlags.NonPublic));
					}
				}

				private static readonly Cache Public = new Cache(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
				private static readonly Cache NonPublic = new Cache(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

				private readonly MemberAccess<P> accessor;
				private Static(PropertyInfo property, bool nonPublic)
					: base(property)
				{
					var valueParam = Parameter(property.PropertyType.MakeByRefType());
					var actionParam = Parameter(typeof(MemberAction));

					var getter = property.GetGetMethod(nonPublic);
					var setter = property.GetSetMethod(nonPublic);

					if (getter is null) //write-only
						accessor = Lambda<MemberAccess<P>>(MemberAccess.GetOrSetValue(actionParam, null, Call(null, setter, valueParam)),
							valueParam,
							actionParam).Compile();
					else if (setter is null) //read-only
						accessor = Lambda<MemberAccess<P>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Call(null, getter)), null),
							valueParam,
							actionParam).Compile();
					else //read-write
						accessor = Lambda<MemberAccess<P>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Call(null, getter)), Call(null, setter, valueParam)),
							valueParam,
							actionParam).Compile();
				}

				public new Method<MemberAccess.Reader<P>> GetGetMethod(bool nonPublic)
				{
					var getter = base.GetGetMethod(nonPublic);
					return getter == null ? null : Method<MemberAccess.Reader<P>>.Static.GetOrNull(getter.Name, nonPublic);
				} 

				public new Method<MemberAccess.Reader<P>> GetMethod
				{
					get
					{
						var getter = base.GetMethod;
						return getter == null ? null : Method<MemberAccess.Reader<P>>.Static.GetOrNull(getter.Name, !getter.IsPublic);
					}
				}
				public new Method<MemberAccess.Writer<P>> SetMethod 
				{ 
					get
					{
						var setter = base.SetMethod;
						return setter == null ? null : Method<MemberAccess.Writer<P>>.Static.GetOrNull(setter.Name, !setter.IsPublic);
					}
				}

				public new Method<MemberAccess.Writer<P>> GetSetMethod(bool nonPublic)
				{
					var setter = base.GetSetMethod(nonPublic);
					return setter == null ? null : Method<MemberAccess.Writer<P>>.Static.GetOrNull(setter.Name, nonPublic);
				}

				/// <summary>
				/// Gets or sets property value.
				/// </summary>
				public P Value
				{
					[MethodImpl(MethodImplOptions.AggressiveInlining)]
					get => accessor.GetValue();
					[MethodImpl(MethodImplOptions.AggressiveInlining)]
					set => accessor.SetValue(value);
				}

				public static explicit operator P(Static property) => property.Value;

				public static implicit operator MemberAccess<P>(Static property) => property?.accessor;

				/// <summary>
				/// Gets static property.
				/// </summary>
				/// <param name="propertyName">Name of property.</param>
				/// <param name="nonPublic">True to reflect non-public property.</param>
				/// <returns>Static property; or null, if property doesn't exist.</returns>
				public static Static GetOrNull(string propertyName, bool nonPublic = false) 
					=> (nonPublic ? NonPublic : Public).GetOrCreate(propertyName);
				
				/// <summary>
				/// Gets static property.
				/// </summary>
				/// <param name="propertyName">Name of property.</param>
				/// <param name="nonPublic">True to reflect non-public property.</param>
				/// <typeparam name="E">Type of exception to throw if property doesn't exist.</typeparam>
				/// <returns>Static property.</returns>
				public static Static GetOrThrow<E>(string propertyName, bool nonPublic = false)
					where E: Exception, new()
					=> GetOrNull(propertyName, nonPublic) ?? throw new E();

				/// <summary>
				/// Gets static property.
				/// </summary>
				/// <param name="propertyName">Name of property.</param>
				/// <param name="exceptionFactory">A factory used to produce exception.</param>
				/// <param name="nonPublic">True to reflect non-public property.</param>
				/// <typeparam name="E">Type of exception to throw if property doesn't exist.</typeparam>
				/// <returns>Static property.</returns>
				public static Static GetOrThrow<E>(string propertyName, Func<string, E> exceptionFactory, bool nonPublic = false)
					where E: Exception
					=> GetOrNull(propertyName, nonPublic) ?? throw exceptionFactory(propertyName);

				/// <summary>
				/// Gets static property.
				/// </summary>
				/// <param name="propertyName">Name of property.</param>
				/// <param name="nonPublic">True to reflect non-public property.</param>
				/// <returns>Static property.</returns>
				/// <exception cref="MissingPropertyException">Property doesn't exist.</exception>
				public static Static Get(string propertyName, bool nonPublic = false)
					=> GetOrThrow(propertyName, MissingPropertyException.Create<T, P>, nonPublic);
			}

			/// <summary>
			/// Provides typed access to the instance property.
			/// </summary>
			public sealed class Instance: Property<P>, IProperty<T, P>
			{
				private sealed class Cache : MemberCache<PropertyInfo, Instance>
				{
					private readonly BindingFlags flags;

					internal Cache(BindingFlags flags) => this.flags = flags;

					private protected override Instance CreateMember(string propertyName)
					{
						var property = RuntimeType.GetProperty(propertyName, flags);
						return property == null ? null : new Instance(property, flags.HasFlag(BindingFlags.NonPublic));
					}
				}

				private static readonly Cache Public = new Cache(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
				private static readonly Cache NonPublic = new Cache(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

				private readonly MemberAccess<T, P> accessor;

				private Instance(PropertyInfo property, bool nonPublic)
					: base(property)
				{
					var instanceParam = Parameter(RuntimeType.MakeByRefType());
					var valueParam = Parameter(property.PropertyType.MakeByRefType());
					var actionParam = Parameter(typeof(MemberAction));

					var getter = property.GetGetMethod(nonPublic);
					var setter = property.GetSetMethod(nonPublic);

					if (getter is null) //write-only
						accessor = Lambda<MemberAccess<T, P>>(MemberAccess.GetOrSetValue(actionParam, null, Call(instanceParam, setter, valueParam)),
							instanceParam,
							valueParam,
							actionParam).Compile();
					else if (setter is null) //read-only
						accessor = Lambda<MemberAccess<T, P>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Call(instanceParam, getter)), null),
						instanceParam,
							valueParam,
							actionParam).Compile();
					else //read-write
						accessor = Lambda<MemberAccess<T, P>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Call(instanceParam, getter)), Call(instanceParam, setter, valueParam)),
							instanceParam,
							valueParam,
							actionParam).Compile();
				}

				public new Method<MemberAccess.Reader<T, P>> GetGetMethod(bool nonPublic)
				{
					var getter = base.GetGetMethod(nonPublic);
					return getter == null ? null : Method<MemberAccess.Reader<T, P>>.Instance.GetOrNull(getter.Name, nonPublic);
				} 

				public new Method<MemberAccess.Reader<T, P>> GetMethod
				{
					get
					{
						var getter = base.GetMethod;
						return getter == null ? null : Method<MemberAccess.Reader<T, P>>.Instance.GetOrNull(getter.Name, !getter.IsPublic);
					}
				}
				public new Method<MemberAccess.Writer<T, P>> SetMethod 
				{ 
					get
					{
						var setter = base.SetMethod;
						return setter == null ? null : Method<MemberAccess.Writer<T, P>>.Instance.GetOrNull(setter.Name, !setter.IsPublic);
					}
				}

				public new Method<MemberAccess.Writer<T, P>> GetSetMethod(bool nonPublic)
				{
					var setter = base.GetSetMethod(nonPublic);
					return setter == null ? null : Method<MemberAccess.Writer<T, P>>.Instance.GetOrNull(setter.Name, nonPublic);
				}

				public static implicit operator MemberAccess<T, P>(Instance property) => property?.accessor;

				/// <summary>
				/// Gets or sets property value.
				/// </summary>
				/// <param name="owner">Property instance.</param>
				/// <returns>Property value.</returns>
				public P this[in T owner]
				{
					[MethodImpl(MethodImplOptions.AggressiveInlining)]
					get => accessor.GetValue(in owner);
					[MethodImpl(MethodImplOptions.AggressiveInlining)]
					set => accessor.SetValue(in owner, value);
				}

				/// <summary>
				/// Gets instance property.
				/// </summary>
				/// <param name="propertyName">Name of property.</param>
				/// <param name="nonPublic">True to reflect non-public property.</param>
				/// <returns>Static instance; or null, if property doesn't exist.</returns>
				public static Instance GetOrNull(string propertyName, bool nonPublic = false)
					=> (nonPublic ? NonPublic : Public).GetOrCreate(propertyName);
				
				/// <summary>
				/// Gets instance property.
				/// </summary>
				/// <param name="propertyName">Name of property.</param>
				/// <param name="nonPublic">True to reflect non-public property.</param>
				/// <typeparam name="E">Type of exception to throw if property doesn't exist.</typeparam>
				/// <returns>Instance property.</returns>
				public static Instance GetOrThrow<E>(string propertyName, bool nonPublic = false)
					where E: Exception, new()
					=> GetOrNull(propertyName, nonPublic) ?? throw new E();

				/// <summary>
				/// Gets instance property.
				/// </summary>
				/// <param name="propertyName">Name of property.</param>
				/// <param name="exceptionFactory">A factory used to produce exception.</param>
				/// <param name="nonPublic">True to reflect non-public property.</param>
				/// <typeparam name="E">Type of exception to throw if property doesn't exist.</typeparam>
				/// <returns>Instance property.</returns>
				public static Instance GetOrThrow<E>(string propertyName, Func<string, E> exceptionFactory, bool nonPublic = false)
					where E: Exception
					=> GetOrNull(propertyName, nonPublic) ?? throw exceptionFactory(propertyName);

				/// <summary>
				/// Gets instance property.
				/// </summary>
				/// <param name="propertyName">Name of property.</param>
				/// <param name="nonPublic">True to reflect non-public property.</param>
				/// <returns>Static property.</returns>
				/// <exception cref="MissingPropertyException">Property doesn't exist.</exception>
				public static Instance Get(string propertyName, bool nonPublic = false)
					=> GetOrThrow(propertyName, MissingPropertyException.Create<T, P>, nonPublic);
			}
		}

		/// <summary>
		/// Provides typed access to the event declared in type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="H">Type of event handler.</typeparam>
		public abstract class Event<H>: EventInfo, IEvent, IEquatable<Event<H>>, IEquatable<EventInfo>
			where H: MulticastDelegate
		{
			private readonly EventInfo @event;

			private protected Event(EventInfo @event)
			{
				this.@event = @event;
			}

			EventInfo IMember<EventInfo>.RuntimeMember => @event;

			public sealed override Type DeclaringType => @event.DeclaringType;

			public sealed override MemberTypes MemberType => @event.MemberType;

			public sealed override string Name => @event.Name;

			public sealed override Type ReflectedType => @event.ReflectedType;

			public sealed override object[] GetCustomAttributes(bool inherit) => @event.GetCustomAttributes(inherit);
        	public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => @event.GetCustomAttributes(attributeType, inherit);

			public sealed override bool IsDefined(Type attributeType, bool inherit) => @event.IsDefined(attributeType, inherit);

			public sealed override int MetadataToken => @event.MetadataToken;

			public sealed override Module Module => @event.Module;

			public sealed override IList<CustomAttributeData> GetCustomAttributesData() => @event.GetCustomAttributesData();

			public sealed override IEnumerable<CustomAttributeData> CustomAttributes => @event.CustomAttributes;

			public sealed override EventAttributes Attributes => @event.Attributes;

			public sealed override bool IsMulticast => @event.IsMulticast;

			public sealed override Type EventHandlerType => @event.EventHandlerType;

			public sealed override MethodInfo AddMethod => @event.AddMethod;
        	public sealed override MethodInfo RaiseMethod => @event.RaiseMethod;
       	 	public sealed override MethodInfo RemoveMethod => @event.RemoveMethod;

			public sealed override MethodInfo GetAddMethod(bool nonPublic) => @event.GetAddMethod(nonPublic);

			public sealed override MethodInfo GetRemoveMethod(bool nonPublic) => @event.GetRemoveMethod(nonPublic);

			public sealed override MethodInfo GetRaiseMethod(bool nonPublic) => @event.GetRaiseMethod(nonPublic);

			public sealed override MethodInfo[] GetOtherMethods(bool nonPublic) => @event.GetOtherMethods();

			public static bool operator ==(Event<H> first, Event<H> second)	=> Equals(first, second);

			public static bool operator !=(Event<H> first, Event<H> second) => !Equals(first, second);

			public bool Equals(EventInfo other) => @event == other;

			public bool Equals(Event<H> other) 
				=>  other != null &&
					GetType() == other.GetType() &&
					Equals(other.@event);

			public sealed override bool Equals(object other)
			{
				switch (other)
				{
					case Event<H> @event:
						return Equals(@event);
					case EventInfo @event:
						return Equals(@event);
					default:
						return false;
				}
			}

			public sealed override int GetHashCode() => @event.GetHashCode();

			public sealed override string ToString() => @event.ToString();

			/// <summary>
			/// Provides typed access to the static event.
			/// </summary>		
			public sealed class Static: Event<H>, IEvent<H>
			{
				private sealed class Cache : MemberCache<EventInfo, Static>
				{
					private readonly BindingFlags flags;

					internal Cache(BindingFlags flags) => this.flags = flags;

					private protected override Static CreateMember(string eventName)
					{
						var @event = RuntimeType.GetEvent(eventName, flags);
						return @event == null ? null : new Static(@event, flags.HasFlag(BindingFlags.NonPublic));
					}
				}

				private static readonly Cache Public = new Cache(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
				private static readonly Cache NonPublic = new Cache(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

				private readonly EventAccess<H> accessor;

				private Static(EventInfo @event, bool nonPublic)
					: base(@event)
				{
					var handlerParam = Parameter(@event.EventHandlerType);
					var actionParam = Parameter(typeof(EventAction));

					var addMethod = @event.GetAddMethod(nonPublic);
					var removeMethod = @event.GetRemoveMethod(nonPublic);
					if (addMethod is null)
						accessor = Lambda<EventAccess<H>>(EventAccess.AddOrRemoveHandler(actionParam, null, Call(removeMethod, handlerParam)), handlerParam, actionParam).Compile();
					else if (removeMethod is null)
						accessor = Lambda<EventAccess<H>>(EventAccess.AddOrRemoveHandler(actionParam, Call(addMethod, handlerParam), null), handlerParam, actionParam).Compile();
					else
						accessor = Lambda<EventAccess<H>>(EventAccess.AddOrRemoveHandler(actionParam, Call(addMethod, handlerParam), Call(removeMethod, handlerParam)), handlerParam, actionParam).Compile();
				}

				public static implicit operator EventAccess<H>(Static @event) => @event?.accessor;

				/// <summary>
				/// Add event handler.
				/// </summary>
				/// <param name="handler">An event handler to add.</param>
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				public void AddEventHandler(H handler) => accessor.AddEventHandler(handler);

				public override void AddEventHandler(object target, Delegate handler)
				{
					if(handler is H typedHandler)
						AddEventHandler(typedHandler);
					else 
						base.AddEventHandler(target, handler);
				}

				/// <summary>
				/// Remove event handler.
				/// </summary>
				/// <param name="handler">An event handler to remove.</param>
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				public void RemoveEventHandler(H handler) => accessor.RemoveEventHandler(handler);

				public override void RemoveEventHandler(object target, Delegate handler)
				{
					if(handler is H typedHandler)
						RemoveEventHandler(typedHandler);
					else
						base.RemoveEventHandler(target, handler);
				}

				/// <summary>
				/// Gets static event.
				/// </summary>
				/// <param name="eventName">Name of event.</param>
				/// <param name="nonPublic">True to reflect non-public event.</param>
				/// <returns>Static event; or null, if event doesn't exist.</returns>
				public static Static GetOrNull(string eventName, bool nonPublic = false) 
					=> (nonPublic ? NonPublic : Public).GetOrCreate(eventName);
				
				/// <summary>
				/// Gets static event.
				/// </summary>
				/// <param name="eventName">Name of event.</param>
				/// <param name="nonPublic">True to reflect non-public event.</param>
				/// <typeparam name="E">Type of exception to throw if event doesn't exist.</typeparam>
				/// <returns>Static event.</returns>
				public static Static GetOrThrow<E>(string eventName, bool nonPublic = false)
					where E: Exception, new()
					=> GetOrNull(eventName, nonPublic) ?? throw new E();

				/// <summary>
				/// Gets static event.
				/// </summary>
				/// <param name="eventName">Name of event.</param>
				/// <param name="exceptionFactory">A factory used to produce exception.</param>
				/// <param name="nonPublic">True to reflect non-public event.</param>
				/// <typeparam name="E">Type of exception to throw if event doesn't exist.</typeparam>
				/// <returns>Static event.</returns>
				public static Static GetOrThrow<E>(string eventName, Func<string, E> exceptionFactory, bool nonPublic = false)
					where E: Exception
					=> GetOrNull(eventName, nonPublic) ?? throw exceptionFactory(eventName);

				/// <summary>
				/// Gets static event.
				/// </summary>
				/// <param name="eventName">Name of event.</param>
				/// <param name="nonPublic">True to reflect non-public event.</param>
				/// <returns>Static event.</returns>
				/// <exception cref="MissingEventException">Event doesn't exist.</exception>
				public static Static Get(string eventName, bool nonPublic = false)
					=> GetOrThrow(eventName, MissingEventException.Create<T, H>, nonPublic);
			}

			/// <summary>
			/// Provides typed access to the instance event.
			/// </summary>
			public sealed class Instance: Event<H>, IEvent<T, H>
			{
				private sealed class Cache : MemberCache<EventInfo, Instance>
				{
					private readonly BindingFlags flags;

					internal Cache(BindingFlags flags) => this.flags = flags;

					private protected override Instance CreateMember(string eventName)
					{
						var @event = RuntimeType.GetEvent(eventName, flags);
						return @event == null ? null : new Instance(@event, flags.HasFlag(BindingFlags.NonPublic));
					}
				}

				private static readonly Cache Public = new Cache(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
				private static readonly Cache NonPublic = new Cache(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

				private readonly EventAccess<T, H> accessor;

				private Instance(EventInfo @event, bool nonPublic)
					: base(@event)
				{
					var instanceParam = Parameter(RuntimeType.MakeByRefType());
					var handlerParam = Parameter(@event.EventHandlerType);
					var actionParam = Parameter(typeof(EventAction));

					var addMethod = @event.GetAddMethod(nonPublic);
					var removeMethod = @event.GetRemoveMethod(nonPublic);

					if (addMethod is null)
						accessor = Lambda<EventAccess<T, H>>(
							EventAccess.AddOrRemoveHandler(actionParam, null, Call(instanceParam, removeMethod, handlerParam)),
							instanceParam,
							handlerParam,
							actionParam
							).Compile();
					else if(removeMethod is null)
						accessor = Lambda<EventAccess<T, H>>(
							EventAccess.AddOrRemoveHandler(actionParam, Call(instanceParam, addMethod, handlerParam), null),
							instanceParam,
							handlerParam,
							actionParam
							).Compile();
					else
						accessor = Lambda<EventAccess<T, H>>(
							EventAccess.AddOrRemoveHandler(actionParam, Call(instanceParam, addMethod, handlerParam), Call(instanceParam, removeMethod, handlerParam)),
							instanceParam,
							handlerParam,
							actionParam
							).Compile();
				}

				public static implicit operator EventAccess<T, H>(Instance @event) => @event?.accessor;

				/// <summary>
				/// Add event handler.
				/// </summary>
				/// <param name="instance">Object with declared event.</param>
				/// <param name="handler">An event handler to add.</param>
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				public void AddEventHandler(in T instance, H handler)
					=> accessor.AddEventHandler(in instance, handler);

				public override void AddEventHandler(object target, Delegate handler)
				{
					if(target is T typedTarget && handler is H typedHandler)
						AddEventHandler(typedTarget, typedHandler);
					else 
						base.AddEventHandler(target, handler);
				}

				/// <summary>
				/// Remove event handler.
				/// </summary>
				/// <param name="instance">Object with declared event.</param>
				/// <param name="handler">An event handler to remove.</param>
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				public void RemoveEventHandler(in T instance, H handler)
					=> accessor.RemoveEventHandler(in instance, handler);

				public override void RemoveEventHandler(object target, Delegate handler)
				{
					if(target is T typedTarget && handler is H typedHandler)
						RemoveEventHandler(typedTarget, typedHandler);
					else
						base.RemoveEventHandler(target, handler);
				}

				/// <summary>
				/// Gets instane event.
				/// </summary>
				/// <param name="eventName">Name of event.</param>
				/// <param name="nonPublic">True to reflect non-public event.</param>
				/// <returns>Instance event; or null, if event doesn't exist.</returns>
				public static Instance GetOrNull(string eventName, bool nonPublic = false)
					=> (nonPublic ? NonPublic : Public).GetOrCreate(eventName);
				
				/// <summary>
				/// Gets instance event.
				/// </summary>
				/// <param name="eventName">Name of event.</param>
				/// <param name="nonPublic">True to reflect non-public event.</param>
				/// <typeparam name="E">Type of exception to throw if event doesn't exist.</typeparam>
				/// <returns>Instance event.</returns>
				public static Instance GetOrThrow<E>(string eventName, bool nonPublic = false)
					where E: Exception, new()
					=> GetOrNull(eventName, nonPublic) ?? throw new E();

				/// <summary>
				/// Gets instance event.
				/// </summary>
				/// <param name="eventName">Name of event.</param>
				/// <param name="exceptionFactory">A factory used to produce exception.</param>
				/// <param name="nonPublic">True to reflect non-public event.</param>
				/// <typeparam name="E">Type of exception to throw if event doesn't exist.</typeparam>
				/// <returns>Instance event.</returns>
				public static Instance GetOrThrow<E>(string eventName, Func<string, E> exceptionFactory, bool nonPublic = false)
					where E: Exception
					=> GetOrNull(eventName, nonPublic) ?? throw exceptionFactory(eventName);

				/// <summary>
				/// Gets instance event.
				/// </summary>
				/// <param name="eventName">Name of event.</param>
				/// <param name="nonPublic">True to reflect non-public event.</param>
				/// <returns>Instance event.</returns>
				/// <exception cref="MissingEventException">Event doesn't exist.</exception>
				public static Instance Get(string eventName, bool nonPublic = false)
					=> GetOrThrow(eventName, MissingEventException.Create<T, H>, nonPublic);
			}
		}

		/// <summary>
		/// Provides typed access to the type attribute.
		/// </summary>
		/// <typeparam name="A">Type of attribute.</typeparam>
		public static class Attribute<A>
			where A: Attribute
		{
			/// <summary>
			/// Returns attribute associated with the type <typeparamref name="T"/>.
			/// </summary>
			/// <param name="inherit">True to find inherited attribute.</param>
			/// <param name="condition">Optional predicate to check attribute properties.</param>
			/// <returns>Attribute associated with type <typeparamref name="T"/>; or null, if attribute doesn't exist.</returns>
			public static A GetOrNull(bool inherit = false, Predicate<A> condition = null)
			{
				var attr = RuntimeType.GetCustomAttribute<A>(inherit);
				return attr is null || condition is null || condition(attr) ? attr : null;
			}

			/// <summary>
			/// Returns attribute associated with the type <typeparamref name="T"/>.
			/// </summary>
			/// <param name="inherit">True to find inherited attribute.</param>
			/// <param name="condition">Optional predicate to check attribute properties.</param>
			/// <typeparam name="E">Type of exception to throw if attribute doesn't exist.</typeparam>
			/// <returns>Attribute associated with type <typeparamref name="T"/></returns>
			public static A GetOrThrow<E>(bool inherit = false, Predicate<A> condition = null)
				where E: Exception, new()
				=> GetOrNull(inherit, condition) ?? throw new E();

			/// <summary>
			/// Returns attribute associated with the type <typeparamref name="T"/>.
			/// </summary>
			/// <param name="exceptionFactory">Exception factory.</param>
			/// <param name="inherit">True to find inherited attribute.</param>
			/// <param name="condition">Optional predicate to check attribute properties.</param>
			/// <typeparam name="E">Type of exception to throw if attribute doesn't exist.</typeparam>
			/// <returns>Attribute associated with type <typeparamref name="T"/>.</returns>
			public static A GetOrThrow<E>(Func<E> exceptionFactory, bool inherit = false, Predicate<A> condition = null)
				where E: Exception
				=> GetOrNull(inherit, condition) ?? throw exceptionFactory();

			/// <summary>
			/// Returns attribute associated with the type <typeparamref name="T"/>.
			/// </summary>
			/// <param name="inherit">True to find inherited attribute.</param>
			/// <param name="condition">Optional predicate to check attribute properties.</param>
			/// <returns>Attribute associated with type <typeparamref name="T"/>.</returns>
			/// <exception cref="MissingAttributeException">Event doesn't exist.</exception>
			public static A Get(bool inherit = false, Predicate<A> condition = null) 
				=> GetOrThrow(MissingAttributeException.Create<T, A>, inherit, condition);

			/// <summary>
			/// Get all custom attributes of type <typeparamref name="A"/>.
			/// </summary>
			/// <param name="inherit">True to find inherited attribute.</param>
			/// <param name="condition">Optional predicate to check attribute properties.</param>
			/// <returns>All attributes associated with type <typeparamref name="T"/>.</returns>
			public static IEnumerable<A> GetAll(bool inherit = false, Predicate<A> condition = null)
				=> from attr in RuntimeType.GetCustomAttributes<A>(inherit)
					where condition is null || condition(attr)
					select attr;
		}

		/// <summary>
		/// Provides typed access to the field declared in type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="F">Type of field value.</typeparam>
		public abstract class Field<F>: FieldInfo, IField, IEquatable<Field<F>>, IEquatable<FieldInfo>
		{
			/// <summary>
			/// Provides access to public instance field.
			/// </summary>
			public sealed class Instance : Field<F>, IField<T, F>
			{
				private sealed class PublicCache : MemberCache<FieldInfo, Instance>
				{
					private protected override Instance CreateMember(string eventName)
					{
						var field = RuntimeType.GetField(eventName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
						return field is null || field.FieldType != typeof(F) ?
							null :
							new Instance(field);
					}
				}

				private sealed class NonPublicCache : MemberCache<FieldInfo, Instance>
				{
					private protected override Instance CreateMember(string eventName)
					{
						var field = RuntimeType.GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
						return field is null || field.FieldType != typeof(F) ?
							null :
							new Instance(field);
					}
				}

				private static readonly MemberCache<FieldInfo, Instance> Public = new PublicCache();
				private static readonly MemberCache<FieldInfo, Instance> NonPublic = new NonPublicCache();

				private readonly MemberAccess<T, F> accessor;

				private Instance(FieldInfo field)
					: base(field)
				{
					var instanceParam = Parameter(field.DeclaringType.MakeArrayType());
					var valueParam = Parameter(field.FieldType.MakeByRefType());
					var actionParam = Parameter(typeof(MemberAction));
					if (field.Attributes.HasFlag(FieldAttributes.InitOnly))
						accessor = Lambda<MemberAccess<T, F>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Field(instanceParam, field)), null),
							instanceParam,
							valueParam,
							actionParam).Compile();
					else
						accessor = Lambda<MemberAccess<T, F>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Field(instanceParam, field)), Assign(Field(instanceParam, field), valueParam)),
							instanceParam,
							valueParam,
							actionParam).Compile();
				}

				public static implicit operator MemberAccess<T, F>(Instance field) => field?.accessor;

				public F this[in T instance]
				{
					get => accessor.GetValue(in instance);
					set => accessor.SetValue(in instance, value);
				}

				/// <summary>
				/// Gets instane field.
				/// </summary>
				/// <param name="fieldName">Name of field.</param>
				/// <param name="nonPublic">True to reflect non-public field.</param>
				/// <returns>Instance field; or null, if field doesn't exist.</returns>
				public static Instance GetOrNull(string fieldName, bool nonPublic = false)
					=> (nonPublic ? NonPublic : Public).GetOrCreate(fieldName);

				/// <summary>
				/// Gets instance field.
				/// </summary>
				/// <param name="fieldName">Name of field.</param>
				/// <param name="nonPublic">True to reflect non-public field.</param>
				/// <typeparam name="E">Type of exception to throw if field doesn't exist.</typeparam>
				/// <returns>Instance field.</returns>
				public static Instance GetOrThrow<E>(string fieldName, bool nonPublic = false)
					where E : Exception, new()
					=> GetOrNull(fieldName, nonPublic) ?? throw new E();

				/// <summary>
				/// Gets instance field.
				/// </summary>
				/// <param name="fieldName">Name of field.</param>
				/// <param name="exceptionFactory">A factory used to produce exception.</param>
				/// <param name="nonPublic">True to reflect non-public field.</param>
				/// <typeparam name="E">Type of exception to throw if field doesn't exist.</typeparam>
				/// <returns>Instance field.</returns>
				public static Instance GetOrThrow<E>(string fieldName, Func<string, E> exceptionFactory, bool nonPublic = false)
					where E : Exception
					=> GetOrNull(fieldName, nonPublic) ?? throw exceptionFactory(fieldName);

				/// <summary>
				/// Gets instance field.
				/// </summary>
				/// <param name="fieldName">Name of field.</param>
				/// <param name="nonPublic">True to reflect non-public field.</param>
				/// <returns>Instance field.</returns>
				/// <exception cref="MissingEventException">Field doesn't exist.</exception>
				public static Instance Get(string fieldName, bool nonPublic = false)
					=> GetOrThrow(fieldName, MissingFieldException.Create<T, F>, nonPublic);
			}

			/// <summary>
			/// Provides access to public instance field.
			/// </summary>
			public sealed class Static : Field<F>, IField<F>
			{
				private sealed class PublicCache : MemberCache<FieldInfo, Static>
				{
					private protected override Static CreateMember(string eventName)
					{
						var field = RuntimeType.GetField(eventName, BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
						return field is null || field.FieldType != typeof(F) ?
							null :
							new Static(field);
					}
				}

				private sealed class NonPublicCache : MemberCache<FieldInfo, Static>
				{
					private protected override Static CreateMember(string eventName)
					{
						var field = RuntimeType.GetField(eventName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
						return field is null || field.FieldType != typeof(F) ?
							null :
							new Static(field);
					}
				}

				private static readonly MemberCache<FieldInfo, Static> Public = new PublicCache();
				private static readonly MemberCache<FieldInfo, Static> NonPublic = new NonPublicCache();

				private readonly MemberAccess<F> accessor;

				private Static(FieldInfo field)
					: base(field)
				{
					var valueParam = Parameter(field.FieldType.MakeByRefType());
					var actionParam = Parameter(typeof(MemberAction));
					if (field.Attributes.HasFlag(FieldAttributes.InitOnly))
						accessor = Lambda<MemberAccess<F>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Field(null, field)), null),
							valueParam,
							actionParam).Compile();
					else
						accessor = Lambda<MemberAccess<F>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Field(null, field)), Assign(Field(null, field), valueParam)),
							valueParam,
							actionParam).Compile();
				}

				/// <summary>
				/// Gets or sets field value.
				/// </summary>
				public F Value
				{
					get => accessor.GetValue();
					set => accessor.SetValue(value);
				}

				public static explicit operator F(Static field) => field.Value;

				public static implicit operator MemberAccess<F>(Static field) => field?.accessor;

				/// <summary>
				/// Gets static field.
				/// </summary>
				/// <param name="fieldName">Name of field.</param>
				/// <param name="nonPublic">True to reflect non-public field.</param>
				/// <returns>Static field; or null, if field doesn't exist.</returns>
				public static Static GetOrNull(string fieldName, bool nonPublic = false)
					=> (nonPublic ? NonPublic : Public).GetOrCreate(fieldName);

				/// <summary>
				/// Gets static field.
				/// </summary>
				/// <param name="fieldName">Name of field.</param>
				/// <param name="nonPublic">True to reflect non-public field.</param>
				/// <typeparam name="E">Type of exception to throw if field doesn't exist.</typeparam>
				/// <returns>Static field.</returns>
				public static Static GetOrThrow<E>(string fieldName, bool nonPublic = false)
					where E : Exception, new()
					=> GetOrNull(fieldName, nonPublic) ?? throw new E();

				/// <summary>
				/// Gets static field.
				/// </summary>
				/// <param name="fieldName">Name of field.</param>
				/// <param name="exceptionFactory">A factory used to produce exception.</param>
				/// <param name="nonPublic">True to reflect non-public field.</param>
				/// <typeparam name="E">Type of exception to throw if field doesn't exist.</typeparam>
				/// <returns>Static field.</returns>
				public static Static GetOrThrow<E>(string fieldName, Func<string, E> exceptionFactory, bool nonPublic = false)
					where E : Exception
					=> GetOrNull(fieldName, nonPublic) ?? throw exceptionFactory(fieldName);

				/// <summary>
				/// Gets static field.
				/// </summary>
				/// <param name="fieldName">Name of field.</param>
				/// <param name="nonPublic">True to reflect non-public field.</param>
				/// <returns>Static field.</returns>
				/// <exception cref="MissingEventException">Field doesn't exist.</exception>
				public static Static Get(string fieldName, bool nonPublic = false)
					=> GetOrThrow(fieldName, MissingFieldException.Create<T, F>, nonPublic);
			}

			private readonly FieldInfo field;

			private protected Field(FieldInfo field)
			{
				this.field = field;
			}

			public sealed override Type DeclaringType => field.DeclaringType;

			public sealed override MemberTypes MemberType => field.MemberType;

			public sealed override string Name => field.Name;

			public sealed override Type ReflectedType => field.ReflectedType;

			public sealed override object[] GetCustomAttributes(bool inherit) => field.GetCustomAttributes(inherit);
			public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => field.GetCustomAttributes(attributeType, inherit);

			public sealed override bool IsDefined(Type attributeType, bool inherit) => field.IsDefined(attributeType, inherit);

			public sealed override int MetadataToken => field.MetadataToken;

			public sealed override Module Module => field.Module;

			public sealed override IList<CustomAttributeData> GetCustomAttributesData() => field.GetCustomAttributesData();

			public sealed override IEnumerable<CustomAttributeData> CustomAttributes => field.CustomAttributes;

			public sealed override FieldAttributes Attributes => field.Attributes;

			public sealed override RuntimeFieldHandle FieldHandle => field.FieldHandle;

			public sealed override Type FieldType => field.FieldType;

			public sealed override Type[] GetOptionalCustomModifiers() => field.GetOptionalCustomModifiers();

			public sealed override object GetRawConstantValue() => field.GetRawConstantValue();

			public sealed override Type[] GetRequiredCustomModifiers() => field.GetRequiredCustomModifiers();

			public sealed override object GetValue(object obj) => field.GetValue(obj);

			[CLSCompliant(false)]
			public sealed override object GetValueDirect(TypedReference obj) => field.GetValueDirect(obj);

			public sealed override bool IsSecurityCritical => field.IsSecurityCritical;

			public sealed override bool IsSecuritySafeCritical => field.IsSecuritySafeCritical;

			public sealed override bool IsSecurityTransparent => field.IsSecurityTransparent;

			public sealed override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture)
				=> field.SetValue(obj, value, invokeAttr, binder, culture);

			[CLSCompliant(false)]
			public sealed override void SetValueDirect(TypedReference obj, object value)
				=> field.SetValueDirect(obj, value);

			public bool IsReadOnly => field.Attributes.HasFlag(FieldAttributes.InitOnly);

			FieldInfo IMember<FieldInfo>.RuntimeMember => field;

			public bool Equals(FieldInfo other) => field.Equals(other);

			public bool Equals(Field<F> other) => other != null && Equals(other.field);

			public sealed override int GetHashCode() => field.GetHashCode();

			public sealed override bool Equals(object other)
			{
				switch (other)
				{
					case Field<F> field:
						return Equals(field);
					case FieldInfo field:
						return Equals(field);
					default:
						return false;
				}
			}

			public sealed override string ToString() => field.ToString();

			public static bool operator ==(Field<F> first, Field<F> second) => Equals(first, second);

			public static bool operator !=(Field<F> first, Field<F> second) => !Equals(first, second);
		}

		/// <summary>
		/// Provides typed access to the method declared in type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="D">Delegate type describing method signature.</typeparam>
		public sealed class Method<D>: MethodInfo, IMethod<D>, IEquatable<Method<D>>, IEquatable<MethodInfo>
			where D: class, MulticastDelegate
		{
			private readonly MethodInfo method;
			private readonly D invoker;

			private Method(MethodInfo method, Func<MethodInfo, D> invokerFactory)
			{
				this.method = method;
				invoker = invokerFactory(method);
			}

			public override MethodAttributes Attributes => method.Attributes;
			public override CallingConventions CallingConvention => method.CallingConvention;
			public override bool ContainsGenericParameters => method.ContainsGenericParameters;
			public override Delegate CreateDelegate(Type delegateType) => method.CreateDelegate(delegateType);
			public override Delegate CreateDelegate(Type delegateType, object target) => method.CreateDelegate(delegateType, target);
			public override IEnumerable<CustomAttributeData> CustomAttributes => method.CustomAttributes;
			public override Type DeclaringType => method.DeclaringType;
			public override MethodInfo GetBaseDefinition() => method.GetBaseDefinition();
			public override object[] GetCustomAttributes(bool inherit) => method.GetCustomAttributes(inherit);
			public override object[] GetCustomAttributes(Type attributeType, bool inherit) => method.GetCustomAttributes(attributeType, inherit);
			public override IList<CustomAttributeData> GetCustomAttributesData() => method.GetCustomAttributesData();
			public override Type[] GetGenericArguments() => method.GetGenericArguments();
			public override MethodInfo GetGenericMethodDefinition() => method.GetGenericMethodDefinition();
			public override MethodBody GetMethodBody() => method.GetMethodBody();
			public override MethodImplAttributes GetMethodImplementationFlags() => method.GetMethodImplementationFlags();
			public override ParameterInfo[] GetParameters() => method.GetParameters();
			public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
				=> method.Invoke(obj, invokeAttr, binder, parameters, culture);
			public override bool IsDefined(Type attributeType, bool inherit)
				=> method.IsDefined(attributeType, inherit);
			public override bool IsGenericMethod => method.IsGenericMethod;
			public override bool IsGenericMethodDefinition => method.IsGenericMethodDefinition;
			public override bool IsSecurityCritical => method.IsSecurityCritical;
			public override bool IsSecuritySafeCritical => method.IsSecuritySafeCritical;
			public override bool IsSecurityTransparent => method.IsSecurityTransparent;
			public override MethodInfo MakeGenericMethod(params Type[] typeArguments) => method.MakeGenericMethod(typeArguments);
			public override MemberTypes MemberType => MemberTypes.Method;
			public override int MetadataToken => method.MetadataToken;
			public override RuntimeMethodHandle MethodHandle => method.MethodHandle;
			public override MethodImplAttributes MethodImplementationFlags => method.MethodImplementationFlags;
			public override Module Module => method.Module;
			public override string Name => method.Name;
			public override Type ReflectedType => method.ReflectedType;
			public override ParameterInfo ReturnParameter => method.ReturnParameter;
			public override Type ReturnType => method.ReturnType;
			public override ICustomAttributeProvider ReturnTypeCustomAttributes => method.ReturnTypeCustomAttributes;

			public bool Equals(MethodInfo other) => method == other;
			public bool Equals(Method<D> other) => Equals(other?.method);

			public override bool Equals(object other)
			{
				switch(other)
				{
					case Method<D> method:
						return Equals(method);
					case MethodInfo method:
						return Equals(method);
					default:
						return false;
				}
			}

			public override int GetHashCode() => method.GetHashCode();

			public static implicit operator D(Method<D> method) => method?.invoker;

			MethodInfo IMember<MethodInfo>.RuntimeMember => method;
			D IMethod<MethodInfo, D>.Invoker => invoker;

			public override string ToString() => method.ToString();

			/// <summary>
			/// Provides access to static methods.
			/// </summary>
			public static class Static
			{
				private sealed class Cache : MemberCache<MethodInfo, Method<D>>
				{
					private readonly BindingFlags flags;

					internal Cache(BindingFlags flags) => this.flags = flags;

					private protected override Method<D> CreateMember(string memberName)
					{
						var invokeMethod = Delegates.GetInvokeMethod<D>();
						var targetMethod = RuntimeType.GetMethod(memberName,
							flags,
							Type.DefaultBinder,
							invokeMethod.GetParameterTypes(),
							Array.Empty<ParameterModifier>());
						return targetMethod != null && invokeMethod.ReturnType.IsAssignableFrom(targetMethod.ReturnType) ?
							new Method<D>(targetMethod, Delegates.CreateDelegate<D>) :
							null;
					}
				}

				private static readonly Cache Public = new Cache(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
				private static readonly Cache NonPublic = new Cache(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

				/// <summary>
				/// Gets static method matching to signature of delegate <typeparamref name="D"/>.
				/// </summary>
				/// <param name="nonPublic">True to reflect non-public method.</param>
				/// <returns>Reflected method; or null, if method doesn't exist.</returns>
				public static Method<D> GetOrNull(string methodName, bool nonPublic = false)
					=> (nonPublic ? NonPublic : Public).GetOrCreate(methodName);

				/// <summary>
				/// Gets static method matching to signature of delegate <typeparamref name="D"/>.
				/// </summary>
				/// <param name="nonPublic">True to reflect non-public method.</param>
				/// <typeparam name="E">Type of exception to throw if method doesn't exist.</typeparam>
				/// <returns>Reflected method.</returns>
				public static Method<D> GetOrThrow<E>(string methodName, bool nonPublic = false)
					where E : Exception, new()
					=> GetOrNull(methodName, nonPublic) ?? throw new E();

				/// <summary>
				/// Gets static method matching to signature of delegate <typeparamref name="D"/>.
				/// </summary>
				/// <param name="exceptionFactory">A factory used to produce exception.</param>
				/// <param name="nonPublic">True to reflect non-public method.</param>
				/// <typeparam name="E">Type of exception to throw if method doesn't exist.</typeparam>
				/// <returns>Reflected method.</returns>
				public static Method<D> GetOrThrow<E>(string methodName, Func<string, E> exceptionFactory, bool nonPublic = false)
					where E : Exception
					=> GetOrNull(methodName, nonPublic) ?? throw exceptionFactory(methodName);
			}

			/// <summary>
			/// Provides access to static methods.
			/// </summary>
			public static class Instance
			{
				private sealed class Cache : MemberCache<MethodInfo, Method<D>>
				{
					private readonly BindingFlags flags;

					internal Cache(BindingFlags flags) => this.flags = flags;

					private protected override Method<D> CreateMember(string memberName)
					{
						var invokeMethod = Delegates.GetInvokeMethod<D>();
						var parameters = invokeMethod.GetParameterTypes();
						var thisParam = parameters.FirstOrDefault();
						if(thisParam is null)
							return null;
						var targetMethod = RuntimeType.GetMethod(memberName,
							flags,
							Type.DefaultBinder,
							parameters.RemoveFirst(1),	//remove hidden this parameter
							Array.Empty<ParameterModifier>());
						//this parameter can be passed as REF so handle this situation
						//first parameter should be passed by REF for structure types
						Func<MethodInfo, D> factory;
						if(thisParam.IsByRef)
						{
							thisParam = thisParam.GetElementType();
							var formalParams = parameters.Map(Parameter);
							factory = thisParam.IsValueType ? 
								new Func<MethodInfo, D>(Delegates.CreateDelegate<D>) : 
								method => Lambda<D>(Call(formalParams[0], method, formalParams.RemoveFirst(1)), formalParams).Compile();
						}
						else if(thisParam.IsValueType)
							return null;
						else
							factory = Delegates.CreateDelegate<D>;
						return thisParam == RuntimeType && invokeMethod.ReturnType.IsAssignableFrom(targetMethod.ReturnType) ?
								new Method<D>(targetMethod, factory) :
								null;
					}
				}

				private static readonly Cache Public = new Cache(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
				private static readonly Cache NonPublic = new Cache(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

				/// <summary>
				/// Gets instance method matching to signature of delegate <typeparamref name="D"/>.
				/// </summary>
				/// <param name="nonPublic">True to reflect non-public method.</param>
				/// <returns>Reflected method; or null, if method doesn't exist.</returns>
				public static Method<D> GetOrNull(string methodName, bool nonPublic = false)
					=> (nonPublic ? NonPublic : Public).GetOrCreate(methodName);

				/// <summary>
				/// Gets instance method matching to signature of delegate <typeparamref name="D"/>.
				/// </summary>
				/// <param name="nonPublic">True to reflect non-public method.</param>
				/// <typeparam name="E">Type of exception to throw if method doesn't exist.</typeparam>
				/// <returns>Reflected method.</returns>
				public static Method<D> GetOrThrow<E>(string methodName, bool nonPublic = false)
					where E : Exception, new()
					=> GetOrNull(methodName, nonPublic) ?? throw new E();

				/// <summary>
				/// Gets instance method matching to signature of delegate <typeparamref name="D"/>.
				/// </summary>
				/// <param name="exceptionFactory">A factory used to produce exception.</param>
				/// <param name="nonPublic">True to reflect non-public method.</param>
				/// <typeparam name="E">Type of exception to throw if method doesn't exist.</typeparam>
				/// <returns>Reflected method.</returns>
				public static Method<D> GetOrThrow<E>(string methodName, Func<string, E> exceptionFactory, bool nonPublic = false)
					where E : Exception
					=> GetOrNull(methodName, nonPublic) ?? throw exceptionFactory(methodName);
			}
		}

		/// <summary>
		/// Provides strongly typed way to reflect methods
		/// </summary>
		public static class Method
		{
			/// <summary>
			/// Provides access to static methods without return value.
			/// </summary>
			public static class Static
			{
				public static Method<Action> Get(string methodName, bool nonPublic = false)
					=> Method<Action>.Static.GetOrThrow(methodName, MissingMethodException.CreateAction<T>, nonPublic);

				public static Method<Action<P>> Get<P>(string methodName, bool nonPublic = false)
					=> Method<Action<P>>.Static.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P>, nonPublic);

				public static Method<Action<P1, P2>> Get<P1, P2>(string methodName, bool nonPublic = false)
					=> Method<Action<P1, P2>>.Static.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2>, nonPublic);

				public static Method<Action<P1, P2, P3>> Get<P1, P2, P3>(string methodName, bool nonPublic = false)
					=> Method<Action<P1, P2, P3>>.Static.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3>, nonPublic);

				public static Method<Action<P1, P2, P3, P4>> Get<P1, P2, P3, P4>(string methodName, bool nonPublic = false)
					=> Method<Action<P1, P2, P3, P4>>.Static.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3, P4>, nonPublic);

				public static Method<Action<P1, P2, P3, P4, P5>> Get<P1, P2, P3, P4, P5>(string methodName, bool nonPublic = false)
					=> Method<Action<P1, P2, P3, P4, P5>>.Static.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3, P4, P5>, nonPublic);

				public static Method<Action<P1, P2, P3, P4, P5, P6>> Get<P1, P2, P3, P4, P5, P6>(string methodName, bool nonPublic = false)
					=> Method<Action<P1, P2, P3, P4, P5, P6>>.Static.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3, P4, P5, P6>, nonPublic);

				public static Method<Action<P1, P2, P3, P4, P5, P6, P7>> Get<P1, P2, P3, P4, P5, P6, P7>(string methodName, bool nonPublic = false)
					=> Method<Action<P1, P2, P3, P4, P5, P6, P7>>.Static.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3, P4, P5, P6, P7>, nonPublic);

				public static Method<Action<P1, P2, P3, P4, P5, P6, P7, P8>> Get<P1, P2, P3, P4, P5, P6, P7, P8>(string methodName, bool nonPublic = false)
					=> Method<Action<P1, P2, P3, P4, P5, P6, P7, P8>>.Static.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3, P4, P5, P6, P7, P8>, nonPublic);

				public static Method<Action<P1, P2, P3, P4, P5, P6, P7, P8, P9>> Get<P1, P2, P3, P4, P5, P6, P7, P8, P9>(string methodName, bool nonPublic = false)
					=> Method<Action<P1, P2, P3, P4, P5, P6, P7, P8, P9>>.Static.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3, P4, P5, P6, P7, P8, P9>, nonPublic);

				public static Method<Action<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>> Get<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>(string methodName, bool nonPublic = false)
					=> Method<Action<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>>.Static.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>, nonPublic);
			}

			/// <summary>
			/// Provides access to static methods with return value.
			/// </summary>
			/// <typeparam name="R">Type of return value.</typeparam>
			public static class Static<R>
			{
				public static Method<Func<R>> Get(string methodName, bool nonPublic = false)
					=> Method<Func<R>>.Static.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, R>, nonPublic);

				public static Method<Func<P, R>> Get<P>(string methodName, bool nonPublic = false)
					=> Method<Func<P, R>>.Static.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P, R>, nonPublic);

				public static Method<Func<P1, P2, R>> Get<P1, P2>(string methodName, bool nonPublic = false)
					=> Method<Func<P1, P2, R>>.Static.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, R>, nonPublic);

				public static Method<Func<P1, P2, P3, R>> Get<P1, P2, P3>(string methodName, bool nonPublic = false)
					=> Method<Func<P1, P2, P3, R>>.Static.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, R>, nonPublic);
				
				public static Method<Func<P1, P2, P3, P4, R>> Get<P1, P2, P3, P4>(string methodName, bool nonPublic = false)
					=> Method<Func<P1, P2, P3, P4, R>>.Static.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, P4, R>, nonPublic);
				
				public static Method<Func<P1, P2, P3, P4, P5, R>> Get<P1, P2, P3, P4, P5>(string methodName, bool nonPublic = false)
					=> Method<Func<P1, P2, P3, P4, P5, R>>.Static.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, P4, P5, R>, nonPublic);

				public static Method<Func<P1, P2, P3, P4, P5, P6, R>> Get<P1, P2, P3, P4, P5, P6>(string methodName, bool nonPublic = false)
					=> Method<Func<P1, P2, P3, P4, P5, P6, R>>.Static.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, P4, P5, P6, R>, nonPublic);

				public static Method<Func<P1, P2, P3, P4, P5, P6, P7, R>> Get<P1, P2, P3, P4, P5, P6, P7>(string methodName, bool nonPublic = false)
					=> Method<Func<P1, P2, P3, P4, P5, P6, P7, R>>.Static.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, P4, P5, P6, P7, R>, nonPublic);

				public static Method<Func<P1, P2, P3, P4, P5, P6, P7, P8, R>> Get<P1, P2, P3, P4, P5, P6, P7, P8>(string methodName, bool nonPublic = false)
					=> Method<Func<P1, P2, P3, P4, P5, P6, P7, P8, R>>.Static.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, P4, P5, P6, P7, P8, R>, nonPublic);

				public static Method<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, R>> Get<P1, P2, P3, P4, P5, P6, P7, P8, P9>(string methodName, bool nonPublic = false)
					=> Method<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, R>>.Static.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, R>, nonPublic);

				public static Method<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, R>> Get<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>(string methodName, bool nonPublic = false)
					=> Method<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, R>>.Static.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, P4, P5, P6, P7, P8, P10, R>, nonPublic);
			}

			/// <summary>
			/// Provides access to instance methods without return value.
			/// </summary>
			public static class Instance
			{
				public static Method<Action<T>> Get(string methodName, bool nonPublic = false)
					=> Method<Action<T>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateAction<T>, nonPublic);

				public static Method<Action<T, P>> Get<P>(string methodName, bool nonPublic = false)
					=> Method<Action<T, P>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P>, nonPublic);

				public static Method<Action<T, P1, P2>> Get<P1, P2>(string methodName, bool nonPublic = false)
					=> Method<Action<T, P1, P2>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2>, nonPublic);

				public static Method<Action<T, P1, P2, P3>> Get<P1, P2, P3>(string methodName, bool nonPublic = false)
					=> Method<Action<T, P1, P2, P3>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3>, nonPublic);

				public static Method<Action<T, P1, P2, P3, P4>> Get<P1, P2, P3, P4>(string methodName, bool nonPublic = false)
					=> Method<Action<T, P1, P2, P3, P4>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3, P4>, nonPublic);

				public static Method<Action<T, P1, P2, P3, P4, P5>> Get<P1, P2, P3, P4, P5>(string methodName, bool nonPublic = false)
					=> Method<Action<T, P1, P2, P3, P4, P5>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3, P4, P5>, nonPublic);

				public static Method<Action<T, P1, P2, P3, P4, P5, P6>> Get<P1, P2, P3, P4, P5, P6>(string methodName, bool nonPublic = false)
					=> Method<Action<T, P1, P2, P3, P4, P5, P6>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3, P4, P5, P6>, nonPublic);

				public static Method<Action<T, P1, P2, P3, P4, P5, P6, P7>> Get<P1, P2, P3, P4, P5, P6, P7>(string methodName, bool nonPublic = false)
					=> Method<Action<T, P1, P2, P3, P4, P5, P6, P7>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3, P4, P5, P6, P7>, nonPublic);

				public static Method<Action<T, P1, P2, P3, P4, P5, P6, P7, P8>> Get<P1, P2, P3, P4, P5, P6, P7, P8>(string methodName, bool nonPublic = false)
					=> Method<Action<T, P1, P2, P3, P4, P5, P6, P7, P8>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3, P4, P5, P6, P7, P8>, nonPublic);

				public static Method<Action<T, P1, P2, P3, P4, P5, P6, P7, P8, P9>> Get<P1, P2, P3, P4, P5, P6, P7, P8, P9>(string methodName, bool nonPublic = false)
					=> Method<Action<T, P1, P2, P3, P4, P5, P6, P7, P8, P9>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3, P4, P5, P6, P7, P8, P9>, nonPublic);

				public static Method<Action<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>> Get<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>(string methodName, bool nonPublic = false)
					=> Method<Action<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateAction<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>, nonPublic);
			}

			/// <summary>
			/// Provides access to instance methods without return value.
			/// </summary>
			/// <typeparam name="R">Type of return value.</typeparam>
			public static class Instance<R>
			{
				public static Method<Func<T, R>> Get(string methodName, bool nonPublic = false)
					=> Method<Func<T, R>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, R>, nonPublic);

				public static Method<Func<T, P, R>> Get<P>(string methodName, bool nonPublic = false)
					=> Method<Func<T, P, R>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P, R>, nonPublic);

				public static Method<Func<T, P1, P2, R>> Get<P1, P2>(string methodName, bool nonPublic = false)
					=> Method<Func<T, P1, P2, R>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, R>, nonPublic);

				public static Method<Func<T, P1, P2, P3, R>> Get<P1, P2, P3>(string methodName, bool nonPublic = false)
					=> Method<Func<T, P1, P2, P3, R>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, R>, nonPublic);

				public static Method<Func<T, P1, P2, P3, P4, R>> Get<P1, P2, P3, P4>(string methodName, bool nonPublic = false)
					=> Method<Func<T, P1, P2, P3, P4, R>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, P4, R>, nonPublic);

				public static Method<Func<T, P1, P2, P3, P4, P5, R>> Get<P1, P2, P3, P4, P5>(string methodName, bool nonPublic = false)
					=> Method<Func<T, P1, P2, P3, P4, P5, R>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, P4, P5, R>, nonPublic);

				public static Method<Func<T, P1, P2, P3, P4, P5, P6, R>> Get<P1, P2, P3, P4, P5, P6>(string methodName, bool nonPublic = false)
					=> Method<Func<T, P1, P2, P3, P4, P5, P6, R>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, P4, P5, P6, R>, nonPublic);

				public static Method<Func<T, P1, P2, P3, P4, P5, P6, P7, R>> Get<P1, P2, P3, P4, P5, P6, P7>(string methodName, bool nonPublic = false)
					=> Method<Func<T, P1, P2, P3, P4, P5, P6, P7, R>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, P4, P5, P6, P7, R>, nonPublic);

				public static Method<Func<T, P1, P2, P3, P4, P5, P6, P7, P8, R>> Get<P1, P2, P3, P4, P5, P6, P7, P8>(string methodName, bool nonPublic = false)
					=> Method<Func<T, P1, P2, P3, P4, P5, P6, P7, P8, R>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, P4, P5, P6, P7, P8, R>, nonPublic);

				public static Method<Func<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, R>> Get<P1, P2, P3, P4, P5, P6, P7, P8, P9>(string methodName, bool nonPublic = false)
					=> Method<Func<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, R>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, R>, nonPublic);

				public static Method<Func<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, R>> Get<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>(string methodName, bool nonPublic = false)
					=> Method<Func<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, R>>.Instance.GetOrThrow(methodName, MissingMethodException.CreateFunc<T, P1, P2, P3, P4, P5, P6, P7, P8, P10, R>, nonPublic);
			}
		}
	}
}