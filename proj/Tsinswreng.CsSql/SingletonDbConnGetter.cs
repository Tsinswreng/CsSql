namespace Tsinswreng.CsSql;

using System.Data;

public class SingletonDbConnGetter : IDbConnMgr{
	public SingletonDbConnGetter(IDbConnection DbConn){
		this.DbConn = DbConn;
	}
	IDbConnection DbConn{get;set;}
	public async Task<IDbConnection> GetConn(CT Ct){
		return DbConn;
	}
	public async Task<nil> AfterUsingConn(IDbConnection Conn, CT Ct){
		return NIL;
	}
}
