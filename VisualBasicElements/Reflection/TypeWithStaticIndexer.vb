Imports System

Namespace Reflection
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

    End Class
End Namespace

