#define Impl
namespace Tsinswreng.CsSql;
using System.Data;
using Tsinswreng.CsCtx;

[Doc(@$"Database Function Context")]
public partial class DbFnCtx:
	FnCtx
	,IDbFnCtx
{
	// [BeaKona.AutoInterface(typeof(IFnCtx), IncludeBaseInterfaces = true)]
	// public IFnCtx IFnCtx{get;set;}
	
	public ITxn? Txn{get;set;}

	public IDbConnection? DbConn{get;set;}
	
	//public IDictionary<obj, obj?>? Props{get;set;}
	
	public ICollection<obj?>? ObjsToDispose{get;set;}
#if Impl
	 = new List<obj?>();
#endif
	[Obsolete]
	public u64 BatchSize{get;set;} = 1;

}
