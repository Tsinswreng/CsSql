using System.Data.Common;
using System.Runtime.CompilerServices;
using Tsinswreng.CsTools;

namespace Tsinswreng.CsSql.BaseImpl;

public class BaseResultReader : IResultReader{
	
	public ISqlCmd Cmd{get;set;}
	public DbCommand RawCmd{get;set;}
	public DbDataReader RawReader{get;set;}
	public IDbValConvtr DbValConvtr{get;protected set;}
	public BaseResultReader(
		ISqlCmd Cmd
		,DbCommand RawCmd
		,DbDataReader RawReader
		,IDbValConvtr DbValConvtr
	){
		this.Cmd = Cmd;
		this.RawCmd = RawCmd;
		this.RawReader = RawReader;
		this.DbValConvtr = DbValConvtr;
	}
	
	public virtual async IAsyncEnumerable<
		IAsyncEnumerable<IDictionary<str, obj?>>
	> AsyE2d(
		[EnumeratorCancellation]
		CT Ct
	){
		using var Dl = new DisposableList();
		DbDataReader Reader = RawReader;
		try{
			Dl.Add(Reader);
			if(Cmd is IDisposable disposableCmd){
				Dl.Add(disposableCmd);
			}
		}
		catch (System.Exception e){
			// 原代码的异常封装逻辑，一行不改
			var Err = new DbErr(
				e.Message
				+ "\nSql:\n" + Cmd.Sql
				+ "\nParams" + RawCmd.Parameters.ToReadableString()
				, e
			);
			Dl.Dispose();
			throw Err;
		}

		// do-while 是多结果集的标准循环，先读第一个结果集，再切换
		do{
			// ★ 核心：为【当前结果集】创建一个「内层异步枚举器」，读取当前结果集的所有行
			// 外层直接 yield return 这个内层枚举器，完美实现二维IAsyncEnumerable
			yield return ReadCurrentResultSetRows(Reader, Ct);

			// ★ 切换到下一个结果集，带取消令牌，无则退出循环
		} while (await Reader.NextResultAsync(Ct));
	}

	// ★ 私有内层方法：负责读取【单个结果集】的所有行，返回单行数据的异步枚举
	private async IAsyncEnumerable<IDictionary<str, obj?>> ReadCurrentResultSetRows(
		DbDataReader reader,
		[EnumeratorCancellation]
		CT Ct
	){
		// 读取当前结果集的每一行，和你原IterAsyE的单行读取逻辑完全一致
		while (await reader.ReadAsync(Ct)){
			var RawDict = new Dictionary<str, obj?>();
			for (var i = 0; i < reader.FieldCount; i++){
//TODO 優化異常ʹ訊
// reader.GetName(i)ʃ得ʹ列名ˋ不帶表名、如Select* 後作多表join 遇同名列則褈添ⁿ致錯、Select A.Id, B.Id旹亦然
var k = reader.GetName(i);
var v = DbValConvtr.ToCodeVal(reader.GetValue(i));
				RawDict.Add(k, v);
			}
			yield return RawDict;
		}
	}


	/// 多個結果集ʹ內容ˇ 皆扁平化
	public virtual async IAsyncEnumerable<IDictionary<str, obj?>> AsyE1dSkipNull(
		[EnumeratorCancellation]
		CT Ct
	){
		var d2 = AsyE2d(Ct);
		await foreach (var d1 in d2){
			await foreach (var row in d1){
				yield return row;
			}
		}
	}

	/// 每個結果集保留一個位置；空結果集輸出null
	public virtual async IAsyncEnumerable<IDictionary<str, obj?>?> AsyE1dWithNull(
		[EnumeratorCancellation]
		CT Ct
	){
		var d2 = AsyE2d(Ct);
		await foreach (var d1 in d2.WithCancellation(Ct)){
			var hasAny = false;
			IDictionary<str, obj?>? first = null;
			await foreach (var row in d1.WithCancellation(Ct)){
				if(!hasAny){
					first = row;
					hasAny = true;
				}
			}
			if(!hasAny){
				yield return null;
			}else{
				yield return first;
			}
		}
	}
	
	public async Task<IList<
		IList<IDictionary<str, obj?>>>
	> All2d(CT Ct){
		var ans = new List<IList<IDictionary<str, obj?>>>();
		await foreach(var d1 in AsyE2d(Ct).WithCancellation(Ct)){
			ans.Add(await d1.ToListAsync(Ct));
		}
		return ans;
	}


	public virtual async Task<IList<IDictionary<str, obj?>>> All1dSkipNull(CT Ct){
		return await AsyE1dSkipNull(Ct).ToListAsync(Ct);
	}

}
