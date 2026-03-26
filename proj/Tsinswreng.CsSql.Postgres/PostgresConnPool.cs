namespace Tsinswreng.CsSql.Postgres;

using System.Data;
using Npgsql;
//TODO 缺 連接計數器
public class PostgresConnPool : IDbConnMgr{
	NpgsqlDataSource DataSource;
	public PostgresConnPool(
		NpgsqlDataSource DataSource
	){
		this.DataSource = DataSource;
	}

	public async Task<nil> AfterUsingConn(IDbConnection Conn, CT Ct){
		if(Conn is IAsyncDisposable asyncDisposable){
			await asyncDisposable.DisposeAsync();
		}else{
			Conn.Dispose();
		}
		return NIL;
	}

	public async Task<IDbConnection> GetConn(CT Ct){
		return await DataSource.OpenConnectionAsync(Ct);
	}
}
