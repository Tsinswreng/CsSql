namespace Tsinswreng.CsSql;

public static class ExtnTblMgr{
	extension<TSelf>(TSelf z)
		where TSelf: ITblMgr
	{
		public IList<str> SqlsMkSchema(){
			List<str> R = [];
			foreach(var (Type, Tbl) in z.EntityType_Tbl){
				var U = Tbl.SqlMkTbl();
				R.Add(U);
			}
			return R;
		}
		public str SqlMkSchema(){
			return str.Join("\n", z.SqlsMkSchema());
		}
		public nil AddTbl<T>(ITblSetter<T> TblSetter){
			return z.AddTbl(TblSetter.Tbl);
		}

		public nil AddAgg<TAgg, TRoot, TRootId>(AggReg<TAgg, TRoot, TRootId> Reg)
			where TRoot: class, new()
		{
			return z.AddAgg(Reg);
		}
	}
	extension<TSelf>(TSelf z)
		where TSelf: ITblMgr, new()//有new約束
	{
		public TSelf Clone(){
			var R = new TSelf();
			R.EntityType_Tbl = z.EntityType_Tbl;
			R.AggType_Reg = z.AggType_Reg;
			R.DbStuff = z.DbStuff;
			return R;
		}
	}

}
