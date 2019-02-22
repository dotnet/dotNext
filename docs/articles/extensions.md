Core Extensions
====

# Randomization
Related class: [RandomExtensions](/api/DotNext.RandomExtensions.md)

Extension methods for random data generation extends both classes _System.Random_ and _System.Security.Cryptography.RandomNumberGenerator_.

## Random string generation
Provides a way to generate random string of the given length and set of allowed characters.
```csharp
using System;
using DotNext;

var rand = new Random();
var password = rand.NextString("abc123", 10);   
//now password has 10 characters
//each character is equal to 'a', 'b', 'c', '1', '2' or '3'
```

The same extension method is provided for class _System.Security.Cryptography.RandomNumberGenerator_.

## Random boolean generation
Provides a way to generate boolean value with the given probability
```csharp
using DotNext;

var rand = new Random();
var b = rand.NextBoolean(0.3D); //0.3 is a probability of TRUE value
```

The same extension method is provided for class _System.Security.Cryptography.RandomNumberGenerator_.