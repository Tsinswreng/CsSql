using System.Linq.Expressions;
using System.Collections;
using Tsinswreng.CsTools;
using Tsinswreng.CsPage;
namespace Tsinswreng.CsSql;

public partial class ISqlSplicer<E>: IAutoBindSqlDuplicator{
	public ISqlSplicer<E> Bool(Expression<Func<E, obj?>> GetMember, str Op, IParam Param){
		var memb = Memb(GetMember);
		return AddSeg(QtCol(memb)).AddSeg(Op).AddSeg(Param);
	}
	public ISqlSplicer<E> Bool(str Memb, str Op, out IParam Param){
		Param = Prm(Memb);
		return AddSeg(QtTblCol(Memb)).AddSeg(Op).AddSeg(Param);
	}
	public ISqlSplicer<E> Bool(Expression<Func<E, obj?>> GetMember, str Op, out IParam Param){
		var memb = Memb(GetMember);
		Param = Prm(memb);
		return Bool(GetMember, Op, Param);
	}
	public ISqlSplicer<E> Eq(Expression<Func<E, obj?>> ExprMemb, IParam Right){
		return AddSeg(QtTblWithMemb(ExprMemb)).AddSeg("=").AddSeg(Right);
	}

	public ISqlSplicer<E> Eq(Expression<Func<E, obj?>> ExprMemb, out IParam Right){
		var memb = Memb(ExprMemb);
		Right = Prm(memb);
		return AddSeg(QtTblWithMemb(ExprMemb)).AddSeg("=").AddSeg(Right);
	}

	public ISqlSplicer<E> Bool(
		str BoolOp
		,Expression<Func<E, obj?>> GetMember, str Op, out IParam Param
	){
		AddSeg(BoolOp);
		return Bool(GetMember, Op, out Param);
	}
	public ISqlSplicer<E> And(Expression<Func<E, obj?>> GetMember, str Op, out IParam Param){
		Bool("AND", GetMember, Op, out Param);
		return this;
	}

	public ISqlSplicer<E> AndEq(Expression<Func<E, obj?>> GetMember, out IParam Param){
		return And(GetMember, "=", out Param);
	}
	public ISqlSplicer<E> LimOfst(out IParam Lim, out IParam Ofst){
		var seg = Tbl.SqlMkr.ParamLimOfst(out Lim, out Ofst);
		AddSeg(seg);
		return this;
	}
}