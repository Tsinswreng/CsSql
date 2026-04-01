namespace Tsinswreng.CsSql;

[Doc(@$"Batch Option")]
public interface IOptBatch{
	[Doc(@$"
	duplication of sql join in one CommandText.
	#See[{nameof(ISqlDuplicator)}]
	")]
	public u64 DupliSqlBatchSize{get;set;}
	
	[Doc("IN (@p0, @p1, ...), the size of params count")]
	public u64 InClauseSize{get;set;}
	
}


public class OptBatch:IOptBatch{
	public u64 DupliSqlBatchSize{get;set;} = 500;

	public u64 InClauseSize{get;set;} = 500;
}
