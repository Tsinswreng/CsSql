#define Impl
namespace Tsinswreng.CsSql;


//類型映射與字段映射
public partial class Column: IColumn{
	
	/// 在數據庫中 字段ʹ名
	
	public string DbName { get; set; } = "";
	public str DbType{get;set;} = "";
	public Type? RawClrType{get;set;}
	public Type? UpperClrType{get;set;}
	public IList<str> AdditionalSqls{get;set;}
#if Impl
	= new List<str>();
#endif
	public bool NotNull{get;set;}
	/// 是否由數據庫生成該列的值(自增/IDENTITY 等)。
	/// true 時:INSERT 不寫入該列、建表 DDL 由各 DB 生成對應自增語法。
	public bool IsDbGenerated{get;set;}
	public IUpperTypeMapFn? UpperTypeMapper{get;set;}
	// public Func<object?,object?>? UpperToRaw{get;set;} = (x)=>x;
	// public Func<object?,object?>? RawToUpper{get;set;} = (x)=>x;
}

