namespace Tsinswreng.CsSql;

/// `SchemaHistory` 倉儲的通用輔助函數。
///
/// 這些邏輯與 Local/Biz 業務均無關，因此統一收斂到 `CsSql`。
public static class ExtnSchemaHistoryRepo{
	extension(IRepo<SchemaHistory, i64> z)
	{
		/// 將 `SchemaHistory` 倉儲的 `PropAccessorReg` 統一綁定到 `SqlHelperStrAcc`。
		///
		/// 原因：
		/// - `SchemaHistory` 是 `CsSql` 內建輔助表
		/// - 它的映射不應由 Local/Biz 各自重複配置
		///
		/// 若調用方註冊的不是 `SqlRepo<SchemaHistory, i64>`，直接拋異常，
		/// 以便盡早暴露配置錯誤。
		public IRepo<SchemaHistory, i64> UseCsSqlSrefl(){
			if(z is not SqlRepo<SchemaHistory, i64> SqlRepoSchemaHistory){
				throw new ArgumentException("RepoSchemaHistory must be SqlRepo<SchemaHistory, i64>");
			}
			SqlRepoSchemaHistory.PropAccessorReg = SqlHelperStrAcc.Inst;
			return z;
		}
	}
}
