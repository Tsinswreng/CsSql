using System.Data;

namespace Tsinswreng.CsSql;

[Doc(@$"Database connection manager.")]
public interface IDbConnMgr{
	[Doc(@$"get a connection. it may be from pool or from a singleton.
	depends on implementation.
	")]
	public Task<IDbConnection> GetConn(CT Ct);
	/// 若潙單例連接則不dipose、否則dispose
	/// 歸還連接勿複用業務層ʹ取消標記
	public Task<nil> AfterUsingConn(IDbConnection Conn, CT Ct);
}
