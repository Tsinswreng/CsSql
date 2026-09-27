namespace Tsinswreng.CsSql;

using System.Linq.Expressions;
using Tsinswreng.CsTools;
using Self = ColMkr;
/// A helper class to build a `IColumn` object.
public partial class ColMkr{
	protected ColMkr(){}
	public ITable Table{get;set;}
	public IColumn Column { get; set; }
	public ColMkr(
		ITable Table
		,IColumn Column
	){
		this.Table = Table;
		this.Column = Column;
	}

}

public class ColMkr<TTbl, TRaw, TUpper>:ColMkr{
	public new ITable<TTbl> TableT{get;set;}
	//public IColumn<TTbl, TRaw, TUpper> ColumnT { get; set; }
}


public static class ColMkrExtn{
	extension(ITable z){
		public Self Col(
			str NameInCode
		){
			var col = z.GetCol(NameInCode);
			var R = new ColMkr(z, col);
			return R;
		}

	}

	extension<TEntity>(ITable<TEntity> z){
		public ColMkr<TEntity, obj?, obj?> Col(
			str NameInCode
		){
			var col = z.GetCol(NameInCode);
			var R = new ColMkr<TEntity, obj?, obj?>();
			R.TableT = z;
			R.Table = z;
			R.Column = col;
			return R;
		}

		public ColMkr<TEntity, obj?, obj?> Col(
			Expression<Func<TEntity, obj?>> ExprMemb
		){
			var memb = ToolExpr.GetMemberName(ExprMemb);
			return z.Col<TEntity>(memb);
		}

		public ColMkr<TEntity, TRaw, TUpper> Col<TRaw, TUpper>(
			str NameInCode
		){
			var col = z.GetCol(NameInCode);
			var R = new ColMkr<TEntity, TRaw, TUpper>();
			R.TableT = z;
			R.Column = col;
			return R;
		}


	}

	extension(ColMkr z){

		public IColumn Build(

		){
			return z.Column;
		}

		public Self DbName(
			str NameInDb
		){
			var col = z.Column;
			col.DbName = NameInDb;
			return z;
		}


		public Self Type<TRaw, TUpper>(
			str? TypeNameInDb = null
		){
			if(TypeNameInDb != null){
				z.Column.DbType = TypeNameInDb;
			}
			z.Column.RawClrType = typeof(TRaw);
			z.Column.UpperClrType = typeof(TUpper);
			return z;
		}

		public Self Type<TRaw>(
			str? TypeInDb = null
		){
			return Type<TRaw,TRaw>(z, TypeInDb);
		}


		public Self NotNull(

		){
			z.Column.NotNull = true;
			return z;
		}

		/// 標記該列爲數據庫生成列(自增/IDENTITY 等)。
		/// 標記後:INSERT 時該列不寫入、值由數據庫生成;建表 DDL 由各 DB 生成對應自增語法。
		public Self DbGenerated(
		){
			z.Column.IsDbGenerated = true;
			return z;
		}


	/// 見 HasConversionʹ註
	/// 用強轉 轉作Func<object?, object?>。
	/// 需保證入參強轉不報錯
	/// <typeparam name="TRaw"></typeparam>
	/// <typeparam name="TUpper"></typeparam>
	/// <param name="z"></param>
	/// <param name="UpperToRaw"></param>
	/// <param name="RawToUpper"></param>
	/// <returns></returns>
		public Self MapType<TRaw, TUpper>(
			Func<TRaw,TUpper> RawToUpper//先Raw後Upper、㕥合泛型參數ʹ序
			,Func<TUpper,TRaw> UpperToRaw
			,Func<object?, TRaw>? ObjToRaw = null
			,Func<object?, TUpper>? ObjToUpper = null
		){
			var Fns = UpperTypeMapFnT<nil, nil>.Mk<TRaw, TUpper>(RawToUpper, UpperToRaw, ObjToUpper, ObjToRaw);
			return MapType(z, Fns);
		}

		public Self MapEnumToInt32<TEnum>(

		)where TEnum : struct, Enum{
			z.MapType<i32, TEnum>(
				(raw)=>(TEnum)(obj)(raw)
				,(upper)=>(i32)(obj)upper
				,ObjToRaw: (obj)=>Convert.ToInt32(obj)
			);
			return z;
		}

		public Self MapEnumToStr<TEnum>(

		)where TEnum : struct, Enum{
			z.MapType<str, TEnum>(
				(raw)=>{
					if (Enum.TryParse(typeof(TEnum), raw, ignoreCase: false, out obj? obj)){
						return (TEnum)obj;
					}
					throw new ArgumentException($"'{raw}' is not valid {typeof(TEnum).Name}");
				}
				,(upper)=>upper.ToString()
				//,ObjToRaw: (obj)=>Convert.ToInt32(obj)
			);
			return z;
		}

		[Obsolete]
		public Self MapTypeOld<TRaw, TUpper>(
			Func<TUpper,TRaw> UpperToRaw
			,Func<TRaw,TUpper> RawToUpper
			,Func<object?, TRaw>? ObjToRaw = null
			,Func<object?, TUpper>? ObjToUpper = null
		){
			var Fns = UpperTypeMapFnT<nil, nil>.MkOld<TRaw, TUpper>(UpperToRaw, RawToUpper, ObjToRaw, ObjToUpper);
			return MapType(z, Fns);
		}

		public Self MapType<TRaw, TUpper>(
			IUpperTypeMapFnT<TRaw, TUpper> Fns
		){
			var col = z.Column;
			col.RawClrType = typeof(TRaw);
			col.UpperClrType = typeof(TUpper);
			return HasConv(z, Fns);
		}


	/// 用強轉 轉作Func<object?, object?>。
	/// 需保證入參強轉不報錯
	/// 例
	/// o.SetCol(nameof(IPoBase.Status)).HasConversion<i32, PoStatus>(
	/// 	s=>Convert.ToInt32(s.Value),
	/// 	val=>new PoStatus(val)
	/// );
	/// 用sqlite旹 緣sqlite中 只有i64洏無i32、故實際取出之原始類型潙i64。return ToUpperType((TRaw)x!);則報錯
	/// <typeparam name="TRaw"></typeparam>
	/// <typeparam name="TUpper"></typeparam>
	/// <param name="z"></param>
	/// <param name="UpperToRaw"></param>
	/// <param name="RawToUpper"></param>
	/// <returns></returns>
		public Self HasConv<TRaw, TUpper>(
			Func<TUpper,TRaw> UpperToRaw
			,Func<TRaw,TUpper> RawToUpper
			,Func<object?, TRaw>? ObjToRaw = null
			,Func<object?, TUpper>? ObjToUpper = null
		){
			var Fns = UpperTypeMapFnT<nil, nil>.Mk<TRaw, TUpper>(RawToUpper, UpperToRaw, ObjToUpper, ObjToRaw);
			return HasConv(z, Fns);
		}

		public Self HasConv<TRaw, TUpper>(
			IUpperTypeMapFnT<TRaw, TUpper> Fns
		){
			z.Column.UpperTypeMapper = Fns.ToNonGeneric();
			z.Table.UpperType_DfltMapper.TryAdd(
				typeof(TUpper)
				,z.Column.UpperTypeMapper
			);
			return z;
		}


		public Self AdditionalSqls(
			IEnumerable<str> Sqls
		){
			foreach(var sql in Sqls){
				z.Column.AdditionalSqls.Add(sql);
			}
			return z;
		}


	}

}
