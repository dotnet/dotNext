Ad-hoc User Data
====
This feature allows to dynamically attach user data to arbitrary managed objects (except value types) respecting encapsulation principle. Let's start from simple example of code:
```csharp
using DotNext;
using System.Text;

public static class StringBuilderHelpers
{
    private static readonly UserDataSlot<DateTime> CreationTimeSlot = UserDataSlot<DateTime>.Allocate();

    public static StringBuilder CreateStringBuilder()
    {
        var builder = new StringBuilder();
        builder.GetUserData().Set(CreationTimeSlot, DateTime.Now);
        return builder;
    }

    public static DateTime CreatedAt(this StringBuilder builder) => builder.GetUserData().GetOrSet(CreationTimeSlot);
}

```
Type [UserDataSlot](xref:DotNext.UserDataSlot`1) represents unique slot for placing user data. Its generic type is the type of user data. Encapsulation is guaranteed through uniqueness of the slot. It is possible to declare slot and limit its lexical scope as in example above. Two slots allocated using method _Allocate_ even of the same type are not equal and provides access to different user data.

Method _GetUserData_ is available for any reference type and returns [user data storage](xref:DotNext.UserDataStorage). Lifetime of the attached user data is equal to the owner object. There is no way no save user data storage as field.

User data slot can be removed from the particular object with the data previously associated with it using _Remove_ method. User data storage provides a set of methods for lazy initialization of the user data. The storage is optimized and it will not be created before calling of mutation methods:
* GetOrSet
* Set

Any other storage methods do not create storage if there is no attached user data.