using Tsinswreng.CsTreeTest;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;

namespace Tsinswreng.CsSql.Test.CsSql.Repo;

public partial class TestRepo {
	readonly List<IdTestKv> _getAllIds = new();
	IdTestKv? _getAllSoftDeletedId = null;

	void RegisterGetAll(ITestNode Node) {
		var register = Node.MkTestFnRegister(
			typeof(TestRepo)
			,[typeof(IRepo<TestKv, IdTestKv>)]
			,[]
		);
		var R = register.Register;
		var T = Assert.IsTrue;

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdAdd)];
		R("GetAll_Insert_Multi", async(o)=>{
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var ents = new List<TestKv>();
				for (var i = 0; i < 3; i++) {
					ents.Add(new TestKv{
						Id = new IdTestKv(),
						Owner = IdTestUser.Zero,
						KStr = "get_all_k_" + System.Guid.NewGuid().ToString("N"),
						VStr = "get_all_v_" + System.Guid.NewGuid().ToString("N"),
					});
				}

				var resp = await Repo.OrdAdd(Ctx, AsyE(ents.ToArray()), CT.None);
				T(resp is not null, "BatAdd returned null response");

				_getAllIds.Clear();
				_getAllIds.AddRange(ents.Select(x=>x.Id));
				_getAllSoftDeletedId = _getAllIds[0];
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.SoftDelInId)];
		R("GetAll_SoftDelete_One", async(o)=>{
			if (_getAllSoftDeletedId is null) {
				T(false, "GetAll_Insert_Multi not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var resp = await Repo.SoftDelInId(Ctx, AsyE(_getAllSoftDeletedId!.Value), CT.None);
				T(resp is not null, "SoftDelInId returned null response");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.GetAll)];
		R("GetAll_Should_Exclude_SoftDeleted", async(o)=>{
			if (_getAllIds.Count == 0 || _getAllSoftDeletedId is null) {
				T(false, "GetAll_Insert_Multi not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var gotAsy = Repo.GetAll(Ctx, CT.None);
				var got = new List<TestKv>();
				await foreach (var item in gotAsy) {
					got.Add(item);
				}

				var gotInserted = got.Where(x=>_getAllIds.Contains(x.Id)).ToList();
				T(gotInserted.Count == _getAllIds.Count - 1, $"Expected {_getAllIds.Count - 1} non-deleted inserted rows, got {gotInserted.Count}");
				T(!gotInserted.Any(x=>x.Id.Equals(_getAllSoftDeletedId!.Value)), "GetAll returned a soft-deleted row");
				T(!gotInserted.Any(x=>x.DelAt != 0), "GetAll returned deleted row in inserted subset");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.GetAllWithDel)];
		R("GetAllWithDel_Should_Include_SoftDeleted", async(o)=>{
			if (_getAllIds.Count == 0 || _getAllSoftDeletedId is null) {
				T(false, "GetAll_Insert_Multi not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var gotAsy = Repo.GetAllWithDel(Ctx, CT.None);
				var got = new List<TestKv>();
				await foreach (var item in gotAsy) {
					got.Add(item);
				}

				var gotInserted = got.Where(x=>_getAllIds.Contains(x.Id)).ToList();
				T(gotInserted.Count == _getAllIds.Count, $"Expected {_getAllIds.Count} inserted rows with deleted included, got {gotInserted.Count}");

				var deleted = gotInserted.FirstOrDefault(x=>x.Id.Equals(_getAllSoftDeletedId!.Value));
				T(deleted is not null, "GetAllWithDel should include soft-deleted row");
				T(deleted is not null && deleted.DelAt != 0, "Soft-deleted row should be marked deleted in GetAllWithDel");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdHardDelById)];
		R("GetAll_Cleanup_HardDelete", async(o)=>{
			if (_getAllIds.Count == 0) {
				return NIL;
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				await Repo.OrdHardDelById(Ctx, AsyE(_getAllIds.ToArray()), CT.None);
				return NIL;
			});
		});
	}
}
