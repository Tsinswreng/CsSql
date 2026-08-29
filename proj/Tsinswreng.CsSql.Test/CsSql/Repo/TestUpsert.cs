using Tsinswreng.CsTreeTest;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;

namespace Tsinswreng.CsSql.Test.CsSql.Repo;

public partial class TestRepo {
	readonly List<TestKv> _batExistsUpsertSeed = new();
	readonly List<IdTestKv> _batExistsUpsertCleanupIds = new();
	IdTestKv? _batExistsUpsertSoftDeletedId = null;

	void RegisterBatExistsAndUpsert(ITestNode Node) {
		var register = Node.MkTestFnRegister(
			typeof(TestRepo)
			,[typeof(IRepo<TestKv, IdTestKv>)]
			,[]
		);
		var R = register.Register;
		var T = Assert.IsTrue;

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdAdd)];
		R("BatExistsUpsert_Insert_Seed", async(o)=>{
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var a = new TestKv{
					Id = new IdTestKv(),
					Owner = IdTestUser.Zero,
					KStr = "bat_exists_seed_k_a_" + System.Guid.NewGuid().ToString("N"),
					VStr = "bat_exists_seed_v_a_" + System.Guid.NewGuid().ToString("N"),
				};
				var b = new TestKv{
					Id = new IdTestKv(),
					Owner = IdTestUser.Zero,
					KStr = "bat_exists_seed_k_b_" + System.Guid.NewGuid().ToString("N"),
					VStr = "bat_exists_seed_v_b_" + System.Guid.NewGuid().ToString("N"),
				};

				await Repo.OrdAdd(Ctx, AsyE(a, b), CT.None);

				_batExistsUpsertSeed.Clear();
				_batExistsUpsertSeed.Add(a);
				_batExistsUpsertSeed.Add(b);

				_batExistsUpsertCleanupIds.Clear();
				_batExistsUpsertCleanupIds.Add(a.Id);
				_batExistsUpsertCleanupIds.Add(b.Id);
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdExistsById)];
		R("BatExistsById_Existing_NonExisting_Existing", async(o)=>{
			if (_batExistsUpsertSeed.Count < 2) {
				T(false, "BatExistsUpsert_Insert_Seed not executed");
			}

			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var existingA = _batExistsUpsertSeed[0].Id;
				var existingB = _batExistsUpsertSeed[1].Id;
				var nonExisting = new IdTestKv();
				var ans = Repo.OrdExistsById(Ctx, AsyE(existingA, nonExisting, existingB), CT.None);

				var list = new List<bool>();
				await foreach (var one in ans) {
					list.Add(one);
				}
				T(list.Count == 3, $"Expected 3 bool results, got {list.Count}");
				T(list[0] && !list[1] && list[2], $"Expected [true,false,true], got [{list[0]},{list[1]},{list[2]}]");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdExistsByIdWithDel), nameof(IRepo<TestKv, IdTestKv>.SoftDelInId)];
		R("BatExistsByIdWithDel_SoftDeleted_Should_Be_True", async(o)=>{
			if (_batExistsUpsertSeed.Count < 2) {
				T(false, "BatExistsUpsert_Insert_Seed not executed");
			}

			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var softDeletedId = _batExistsUpsertSeed[1].Id;
				var softDelResp = await Repo.SoftDelInId(Ctx, AsyE(softDeletedId), CT.None);
				T(softDelResp is not null, "SoftDelInId returned null response");
				_batExistsUpsertSoftDeletedId = softDeletedId;

				var ans = Repo.OrdExistsByIdWithDel(Ctx, AsyE(softDeletedId), CT.None);
				var list = new List<bool>();
				await foreach (var one in ans) {
					list.Add(one);
				}
				T(list.Count == 1, $"Expected 1 bool result, got {list.Count}");
				T(list[0], "OrdExistsByIdWithDel should treat soft-deleted row as existing");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdUpsert), nameof(IRepo<TestKv, IdTestKv>.OrdGetByIdWithDel)];
		R("BatUpsert_Insert_And_Update", async(o)=>{
			if (_batExistsUpsertSeed.Count < 2) {
				T(false, "BatExistsUpsert_Insert_Seed not executed");
			}

			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var existed = _batExistsUpsertSeed[0];
				var newOne = new TestKv{
					Id = new IdTestKv(),
					Owner = IdTestUser.Zero,
					KStr = "bat_upsert_new_k_" + System.Guid.NewGuid().ToString("N"),
					VStr = "bat_upsert_new_v_" + System.Guid.NewGuid().ToString("N"),
				};

				var existedUpdated = new TestKv{
					Id = existed.Id,
					Owner = existed.Owner,
					KStr = "bat_upsert_upd_k_" + System.Guid.NewGuid().ToString("N"),
					VStr = "bat_upsert_upd_v_" + System.Guid.NewGuid().ToString("N"),
				};

				await Repo.OrdUpsert(Ctx, AsyE(existedUpdated, newOne), CT.None);

				_batExistsUpsertCleanupIds.Add(newOne.Id);

				var got = Repo.OrdGetByIdWithDel(Ctx, AsyE(existedUpdated.Id, newOne.Id), CT.None);
				var gotList = new List<TestKv?>();
				await foreach (var one in got) {
					gotList.Add(one);
				}
				T(gotList.Count == 2, $"Expected 2 records, got {gotList.Count}");

				var gotUpdated = gotList[0];
				var gotInserted = gotList[1];
				T(gotUpdated is not null, "Expected updated record not null");
				T(gotInserted is not null, "Expected inserted record not null");

				T(gotUpdated is not null && gotUpdated.KStr == existedUpdated.KStr && gotUpdated.VStr == existedUpdated.VStr,
					"Upsert update branch did not update expected fields");
				T(gotInserted is not null && gotInserted.KStr == newOne.KStr && gotInserted.VStr == newOne.VStr,
					"Upsert insert branch did not insert expected fields");

				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdUpsert), nameof(IRepo<TestKv, IdTestKv>.OrdGetByIdWithDel)];
		R("BatUpsert_SoftDeletedSameId_Should_Update_Not_Insert", async(o)=>{
			if (_batExistsUpsertSoftDeletedId is null) {
				T(false, "BatExistsByIdWithDel_SoftDeleted_Should_Be_True not executed");
			}

			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var targetId = _batExistsUpsertSoftDeletedId!.Value;
				var updated = new TestKv{
					Id = targetId,
					Owner = IdTestUser.Zero,
					KStr = "bat_upsert_soft_deleted_k_" + System.Guid.NewGuid().ToString("N"),
					VStr = "bat_upsert_soft_deleted_v_" + System.Guid.NewGuid().ToString("N"),
				};

				await Repo.OrdUpsert(Ctx, AsyE(updated), CT.None);

				var got = Repo.OrdGetByIdWithDel(Ctx, AsyE(targetId), CT.None);
				var gotList = new List<TestKv?>();
				await foreach (var one in got) {
					gotList.Add(one);
				}
				T(gotList.Count == 1, $"Expected 1 record, got {gotList.Count}");

				var gotUpdated = gotList[0];
				T(gotUpdated is not null, "Expected updated soft-deleted record not null");
				T(gotUpdated is not null && gotUpdated.KStr == updated.KStr && gotUpdated.VStr == updated.VStr,
					"Upsert should update existing soft-deleted row by same id");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdHardDelById)];
		R("BatExistsUpsert_Cleanup_HardDelete", async(o)=>{
			if (_batExistsUpsertCleanupIds.Count == 0) {
				return NIL;
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				await Repo.OrdHardDelById(Ctx, AsyE(_batExistsUpsertCleanupIds.ToArray()), CT.None);
				return NIL;
			});
		});
	}
}
