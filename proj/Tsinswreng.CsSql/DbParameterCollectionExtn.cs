using System.Data.Common;
using System.Text;

namespace Tsinswreng.CsSql;

public static class ExtnDbParameterCollection {
	extension<TSelf>(TSelf z)
		where TSelf:DbParameterCollection
	{

		public Dictionary<string, object> ToDictionary(){
			var dict = new Dictionary<string, object>();
			if (z == null || z.Count == 0) {
				return dict;
			}
			foreach (DbParameter param in z){
				// 存储参数的详细信息（键为参数名，值为包含详细属性的匿名对象）
				dict[param.ParameterName] = new{
					Value = param.Value == DBNull.Value ? "[DBNull]" : param.Value,
					DbType = param.DbType,
					Direction = param.Direction,
					Size = param.Size > 0 ? param.Size : (int?)null, // 非字符串类型可能无Size
					Precision = param.Precision > 0 ? param.Precision : (byte?)null,
					Scale = param.Scale > 0 ? param.Scale : (byte?)null
				};
			}
			return dict;
		}


		public string ToReadableString(){
			if (z == null || z.Count == 0) {
				return "";
			}
			var sb = new StringBuilder();
			foreach (DbParameter param in z){
				// 处理值（区分 DBNull 和 null）
				string? valueStr = param.Value ==
					DBNull.Value ? "[DBNull]" :
					param.Value == null ? "[null]" :
					param.Value.ToString()
				;

				// 特殊类型处理
				if (param.Value is byte[] byteArr) {
					valueStr = GetSqlBinaryLiteral(byteArr);
				}

				// 可扩展其他类型（如 DateTime 显示具体格式）
				else if (param.Value is DateTime dt) {
					valueStr = dt.ToString("yyyy-MM-dd HH:mm:ss");
				}
				// 拼接参数信息（参数名=值，类型）
				sb.AppendLine($"{param.ParameterName} = {valueStr}  (type: {param.DbType}, direction: {param.Direction})");
			}
			return sb.ToString().TrimEnd(); // 移除最后一个换行
		}
	}

	// 辅助方法：生成适合SQL的二进制字面量字符串
	private static string GetSqlBinaryLiteral(byte[] byteArr){
		if (byteArr == null || byteArr.Length == 0){
			return "0x0";
		}

		// 长度小于256：显示完整16进制
		if (byteArr.Length < 256){
			return "0x" + BitConverter.ToString(byteArr).Replace("-", "");
		}

		// 长度>=256：显示头部32字节 + 省略号 + 尾部32字节（总64字节预览）
		int headLength = 32;
		int tailLength = 32;
		// 避免数组总长度不足64时的越界（如长度256时，头部32+尾部224会超，这里取实际可用长度）
		headLength = Math.Min(headLength, byteArr.Length);
		tailLength = Math.Min(tailLength, byteArr.Length - headLength);

		// 转换头部和尾部为十六进制
		string headHex = BitConverter.ToString(byteArr, 0, headLength).Replace("-", "");
		string tailHex = BitConverter.ToString(byteArr, byteArr.Length - tailLength, tailLength).Replace("-", "");

		return $"0x{headHex}...{tailHex} ( {byteArr.Length} bytes in total)";
	}


}
