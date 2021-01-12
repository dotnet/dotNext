Imports System.Diagnostics.CodeAnalysis

Namespace Reflection

    <ExcludeFromCodeCoverage>
    Public NotInheritable Class TypeWithStaticIndexer

        Public Shared ReadOnly BackedArray As String() = New String(10) {}

        Public Shared Property MyIndexer(ByVal index As Integer) As String
            Get
                Return BackedArray(index)
            End Get
            Set(value As String)
                BackedArray(index) = value
            End Set
        End Property

        Public Shared Operator ^(ByVal left As TypeWithStaticIndexer, ByVal right As Integer) As Integer
            Return right
        End Operator

    End Class
End Namespace

