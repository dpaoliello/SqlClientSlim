# Release Notes

## 0.0.1-alpha (versus the Official System.Data.SqlClient)

* Turned on Managed SNI for Windows
  * This break integrated authentication
  * ~50% performance regression
* Removed dependencies:
  * System.Collections.NonGeneric
  * System.Console
  * System.Linq
  * System.Threading.ThreadPool
* `SqlErrorCollection` now implements `IReadOnlyList<T>`