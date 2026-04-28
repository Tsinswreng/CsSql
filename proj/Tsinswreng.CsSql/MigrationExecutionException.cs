namespace Tsinswreng.CsSql;

/// 遷移執行失敗時拋出的明確異常。
///
/// 目標：
/// - 讓調用方一眼知道失敗點在 migration，而不是後續業務讀取
/// - 保留 migration 名稱、CreatedMs 與當前 SQL 片段
/// - 內層異常保留原始數據庫/類型轉換錯誤，便於繼續定位
public class MigrationExecutionException: Exception{
	public str? MigrationName{get;}
	public i64? MigrationCreatedMs{get;}
	public str? SqlText{get;}

	public MigrationExecutionException(
		str message
		,Exception innerException
		,str? migrationName = null
		,i64? migrationCreatedMs = null
		,str? sqlText = null
	): base(message, innerException){
		MigrationName = migrationName;
		MigrationCreatedMs = migrationCreatedMs;
		SqlText = sqlText;
	}
}
