namespace Tsinswreng.CsSql.Test.Domains;

using Tsinswreng.CsCore;
using Tsinswreng.CsSql;
using Tsinswreng.CsU128Id;

/// <summary>
/// 測試域強類型 Id 的統一接口。
/// 底層為 UInt128,主鍵存 BLOB(u8[]),由代碼生成(ULID),非自增。
/// 用法照 Ngan.Dict 的 IIdUInt128,但不依賴 Ngan.Dict 的任何類型。
/// </summary>
public partial interface ITestStrongId
	:I_Value<UInt128>
{

}

/// <summary>
/// 強類型 Id 的類型映射與工具擴展。
/// 主鍵在數據庫中存 u8[] BLOB,經此映射在 實體 Id 與 原始位元組 之間轉換。
/// </summary>
public static class ExtnITestStrongId{
	extension<T>(T z)
		where T:struct, ITestStrongId
	{
		public static T Zero => default!;

		/// <summary>產生一個新的 ULID 主鍵(代碼生成、非自增)。</summary>
		public static T NewUlid(){
			var bytes = Ulid.NewUlid().ToByteArray();
			var u128 = ToolU128Id.ByteArrToUInt128(bytes);
			return new T(){Value = u128};
		}

		public u8[] ToByteArr(){
			return ToolU128Id.ToByteArr(z.Value);
		}

		public static T FromByteArr(byte[] bytes){
			var Num = ToolU128Id.ByteArrToUInt128(bytes);
			return new T(){Value = Num};
		}

		/// <summary>CsSql 表列用的類型映射:u8[](BLOB) <-> 強類型 Id。</summary>
		public static IUpperTypeMapFnT<u8[], T> MkTypeMapFn(){
			return UpperTypeMapFnT<u8[], T>.Mk(
				(raw)=>FromByteArr<T>(raw)
				,(upper)=>upper.Value.ToByteArr()
			);
		}
	}
}
