//IRepo 的「批一級形狀」重載(IList 版):與 IRepo.cs 中同名 IAsyncEnumerable 版一一對應、語義一致。
//設計:函數邊界 = 批邊界——吃 IList 的函數內部零業務分批,傳多少元素就執行多少
//(同構批量拼 N 組 SQL / IN 同理),一批的規模上限由執行層(庫原語的默認策略)負責兜底;
//流衹出現在調用方來源側,由批原語(SqlFlow.Batches / BatchesInOnly)在調用點切流後逐批調入本文件的方法。
//
//【樣板階段(2026-09-01)】本文件暫只保留 4 個代表性重載
//(寫最簡 / 查最簡 / 雙參數流 / Agg 讀),供用戶確認形狀;
//其餘 18 個(GetInId / OrdUpsert / SoftDel / 寫向 Agg 等)在全量遷移時再加回,格式照抄本文件。
namespace Tsinswreng.CsSql;

using Tsinswreng.CsPage;
using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;

public partial interface IRepo<TEntity, TId>
{
	#region 樣板:寫·最簡(同構批量 INSERT)

	/// IList 版 OrdAdd:把 List 內的全部實體一次性拼進同構批量 INSERT 並執行完畢。
	/// 語義承諾:
	/// - 函數返回 = 本批全部生效(約束/衝突等異常直接向上拋,不留半批返回的狀態);
	/// - 空列表是合法的無操作(不發 SQL、直接返回成功)。
	[Doc(@$"IList 版:`{nameof(OrdAdd)}` 的批一級形狀。本批全部執行完畢才返回(衝突/約束違反即拋)。")]
	public Task<IRespBatInsert> OrdAdd(
		IDbFnCtx Ctx, IList<TEntity> Ents, CT Ct
	);

	#endregion

	#region 樣板:查·最簡(IN + 位置對齊)

	/// IList 版 OrdGetByIdWithDel:按入參 Id 列表做一次 IN 查詢,含軟刪行。
	/// 語義承諾(與流式版一致):
	/// - 出參 List 與入參 Ids 一一對應(位置對齊):重複的 Id 返回重複的實體;
	/// - 查無的 Id 對應位置補 null;
	/// - 空列表返回空列表(不發 SQL)。
	[Doc(@$"IList 版:`{nameof(OrdGetByIdWithDel)}` 的批一級形狀、含軟刪。
	與入參位置一一對應、查無補 null。")]
	public Task<IList<TEntity?>> OrdGetByIdWithDel(
		IDbFnCtx Ctx, IList<TId> Ids, CT Ct
	);

	#endregion

	#region 樣板:雙參數流(UPDATE by Db Dict,Ids / Dicts 成對)

	/// IList 版 OrdUpdByDbDict:Ids 與 Dicts 成對做 UPDATE(每對一條,拼進同一次命令執行完畢)。
	/// 語義承諾:
	/// - Ids 與 Dicts 長度必須相等,否則拋 ArgumentException(不執行任何 UPDATE);
	/// - 【支持異構字典】每對 Dict 的鍵集可以互不相同——各行只更新自己字典裏出現的列,
	///   缺的列保持原樣。正因如此,本方法的 SQL 無法模板化重複(AutoBatch/FnSqlDuplicator
	///   只適用於「各份 SET 列集一致」的同構批量),實現必須逐對手拼,這是異構語義的必然形狀;
	/// - Dict 以「Db 列名 → 原值」形式給入(列名帶 Db 風格或代碼風格都可,UPPER 轉換由實現處理);
	/// - 空的 Dict(沒有可更新的列)那一對被跳過,不影響其它對。
	[Doc(@$"IList 版:`{nameof(OrdUpdByDbDict)}` 的批一級形狀。Ids 與 Dicts 個數須相等。
	支持異構字典(各行更新的列集可不同)。")]
	public Task<IRespBatUpd> OrdUpdByDbDict(
		IDbFnCtx Ctx, IList<TId> Ids, IList<IStr_Any> Dicts, CT Ct
	);

	#endregion

	#region 樣板:Agg 讀(根 + include 資產,位置對齊)

	/// IList 版 OrdGetAggByIdWithDel:按入參 Id 列表一次裝配聚合(根實體 + 全部 include 資產),含軟刪。
	/// 語義承諾(與流式版一致):
	/// - 出參 List 與入參 Ids 一一對應(位置對齊),查無的 Id 對應位置補 null;
	/// - 每個 Id 只返回一個聚合實例;OneToOne include 若查出重複行會拋異常(數據不一致)。
	[Doc(@$"IList 版:`{nameof(OrdGetAggByIdWithDel)}` 的批一級形狀,含軟刪。位置一一對應、查無補 null。")]
	public Task<IList<TAgg?>> OrdGetAggByIdWithDel<TAgg>(
		IDbFnCtx Ctx, IList<TId> Ids, CT Ct
	)where TAgg: class;

	#endregion
}
