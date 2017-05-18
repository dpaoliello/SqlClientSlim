# Release Notes

## vNext

## 0.2.0
* Updated to v1.1.1 of the official System.Data.SqlClient
* More perf improvements (~20% performance regression versus NGENed desktop SqlClient)

## 0.0.1-alpha (versus the Official System.Data.SqlClient)

* Turned on Managed SNI for Windows
  * This breaks integrated authentication
  * ~50% performance regression versus NGENed desktop SqlClient
* Removed dependencies:
  * System.Collections.NonGeneric
  * System.Console
  * System.Linq
  * System.Threading.ThreadPool
  * System.Reflection.TypeExtensions
* `SqlErrorCollection` now implements `IReadOnlyList<T>`