using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using Management;
using Modular.States;

namespace Modular{
	public class ModularBuildingSystem {

		// private variables
		public FinitStateMachine<ModularBuildingSystem> Statemachine;
		public MonoBehaviour Mono;

		public ModularBuildingSystem(MonoBehaviour Mono){
			Statemachine = new FinitStateMachine<ModularBuildingSystem> ();
			Statemachine.changeState (new ModularSpectate(),this);
			this.Mono = Mono;
		}

		public void Update(){
			Statemachine.Update(); // update statemachine
		}

		public void SelectPlacableObjects(GameObject[] Objects,object[] CustomData = default(object[])){
			List<ModularPlacableObject> ModularPieces = new List<ModularPlacableObject>();
			for(int i = 0; i < Objects.Length; i++){
				ModularPlacableObject Placable = (ModularPlacableObject)Objects [i].GetComponent(typeof(ModularPlacableObject));
				if (Placable != null) {
					ModularPieces.Add (Placable);
				}
			}
			SelectPlacableObjects (ModularPieces.ToArray(),CustomData);
		}
		public void SelectPlacableObjects(ModularPlacableObject[] Placables,object[] CustomData = default(object[])){
			List<object> GenData = new List<object> ();
			GenData.Add (this);
			GenData.Add (Placables);

			if (CustomData != null) {
				for (int i = 0; i < CustomData.Length; i++) {
					GenData.Add (CustomData [i]); // add custom data to gendata
				}
			}

			Statemachine.changeState (new SelectPlacableObject(), GenData);
		}

		public void PlaceModularPieceAsSet(ModularPieceData Piece,Vector3 Scale,System.Func<ModularPlacableObject,bool> CanPlace,Vector3 RotationOffset = default(Vector3),Vector3 InitRotation = default(Vector3),Vector3 InitPosition = default(Vector3),object[] ExtraData = default(object[]),bool Interrupt = false){
			ModularSet Set = InitNewModularSet (); // create new modular set
			Set.InitializeEditable (); // init as editable
			GameManager.I.Modular.ScopedSet = Set; // set new scoped set
			ModularPlacableObject NewPlacableObject = (ModularPlacableObject)InitStoreItem (Piece,Scale,RotationOffset, InitRotation, InitPosition); // init modular piece afther setting the scoped set

			PlaceModularPlacable (NewPlacableObject,(object[] data)=>{
				// on object is placed
				ModularPiece PlacedPiece = (ModularPiece)data.Find<ModularPiece>(); // get modular piece
				LocalGridSystem LocalGrid = (LocalGridSystem)data.Find<LocalGridSystem>(); // get last local grid
				PlacePlacableObject.PlaceObjectCache Cache = (PlacePlacableObject.PlaceObjectCache)data.Find<PlacePlacableObject.PlaceObjectCache>(); // get last state cache
				PlacedPiece.InitializeEditable(); // initialize editable modular piece
				PlacedPiece.OnPlaced(); // call on placed callback

				// Economy
				int Price = GameManager.I.Economy.EvaluateModularPiece(Piece);
				GameManager.I.Economy.RemoveMoney(Price);
				GameManager.I.Economy.SpawnMoneyIndicator(PlacedPiece.transform.position,Price);

				// add piece to set
				Set.AddModularPiece(PlacedPiece);
				Set.Name = PlacedPiece.name;
				Set.FinalizeSet(GameManager.I.Modular.ModularPieceLayer, true); // finalize set

				// place new modular piece with last rotation
				PlaceModularPieceAsSet(Piece,Scale,CanPlace,PlacedPiece.RotationOffset.eulerAngles,PlacedPiece.NoneOffsettedRotation.eulerAngles,PlacedPiece.transform.position,new object[]{LocalGrid,Cache});
			},CanPlace,(Data)=>{
				// on canceled
				GameManager.I.Modular.ScopedSet.DeregisterPlacingObject(); // de register object
				ModularPlacableObject PlacableObject = (ModularPlacableObject)Data.Find<ModularPlacableObject>();
				PlacableObject.Destroy(); // destroy on cancle
				Set.FinalizeSet(GameManager.I.Modular.ModularPieceLayer, false); // finalize set

				Statemachine.changeState(new ModularSpectate(),this);
			},SnapTypes.Default,ExtraData,Interrupt); // place new created store modular piece
			Set.RegisterPlacingObject(NewPlacableObject); // register new placable object to be placed inside building
		}

		public void PlaceNewModularPiece(ModularPieceData Piece,Vector3 Scale,System.Func<ModularPlacableObject,bool> CanPlace,Vector3 RotationOffset = default(Vector3),Vector3 InitRotation = default(Vector3),Vector3 InitPosition = default(Vector3),object[] ExtraData = default(object[]),bool Interrupt = false){
			ModularPlacableObject NewPlacableObject = (ModularPlacableObject)InitStoreItem (Piece,Scale,RotationOffset, InitRotation, InitPosition);
			PlaceModularPlacable (NewPlacableObject,(object[] data)=>{
				// on object is placed
				ModularPiece PlacedPiece = (ModularPiece)data.Find<ModularPiece>(); // get modular piece
				LocalGridSystem LocalGrid = (LocalGridSystem)data.Find<LocalGridSystem>(); // get last local grid
				PlacePlacableObject.PlaceObjectCache Cache = (PlacePlacableObject.PlaceObjectCache)data.Find<PlacePlacableObject.PlaceObjectCache>(); // get last state cache
				PlacedPiece.InitializeEditable(); // initialize editable modular piece
				PlacedPiece.OnPlaced(); // call on placed callback

				// Economy
				int Price = GameManager.I.Economy.EvaluateModularPiece(Piece);
				GameManager.I.Economy.RemoveMoney(Price);
				GameManager.I.Economy.SpawnMoneyIndicator(PlacedPiece.transform.position,Price);

				// add piece to set
				GameManager.I.Modular.ScopedSet.AddModularPiece(PlacedPiece);

				// place new modular piece with last rotation
				PlaceNewModularPiece(Piece,Scale,CanPlace,PlacedPiece.RotationOffset.eulerAngles,PlacedPiece.NoneOffsettedRotation.eulerAngles,PlacedPiece.transform.position,new object[]{LocalGrid,Cache});
			},CanPlace	,(Data)=>{
				// on canceled
				GameManager.I.Modular.ScopedSet.DeregisterPlacingObject(); // de register object
				ModularPlacableObject PlacableObject = (ModularPlacableObject)Data.Find<ModularPlacableObject>();
				PlacableObject.Destroy(); // destroy on cancle

				Statemachine.changeState(new ModularSpectate(),this);
			},SnapTypes.Default,ExtraData,Interrupt); // place new created store modular piece
			GameManager.I.Modular.ScopedSet.RegisterPlacingObject(NewPlacableObject); // register new placable object to be placed inside building
		}

		public void PlaceModularPiece(ModularPiece Piece,Delegates.GenericDataCallbak PlacedCallback,System.Action<object[]> OnCanceled,System.Func<ModularPlacableObject,bool> CanPlace){
			PlaceModularPlacable ((ModularPlacableObject)Piece,PlacedCallback,CanPlace,OnCanceled);
		}

		public void ReplaceModularPlacable(ModularPlacableObject Placable){
			PlaceModularPlacable (Placable, (object[] data) => {
				ModularPlacableObject Placed = data.Find<ModularPlacableObject>();
				Placed.OnPlaced();

				// placed
				Statemachine.changeState (new ModularSpectate(),this);
				GameManager.I.Utils.ChangeUndoable(Placed.gameObject); // register undo change
			},(ModularPlacableObject Obj)=>{
				return true;
			}, (data) => {
				ModularPlacableObject Placed = data.Find<ModularPlacableObject>();
				Placed.OnPlaced();

				// cancel
				Statemachine.changeState (new ModularSpectate(),this);
				GameManager.I.Utils.ChangeUndoable(Placed.gameObject); // register undo change
			});
			Placable.OnDeplaced(); // deplace placable
			Placable.RemoveFromGrid (); 
		}

		public void PlaceModularPlacable(
			ModularPlacableObject ModularPlacable,
			Delegates.GenericDataCallbak PlacedCallback,
			System.Func<ModularPlacableObject,bool> CanPlace,
			System.Action<object[]> OnCanceled,
			SnapTypes InitSnapType = SnapTypes.Default,
			object[] ExtraData = default(object[]),
			bool Interrupt = false
		){
			List<object> GenData = new List<object> ();
			GenData.Add (this);
			GenData.Add (ModularPlacable);
			GenData.Add (PlacedCallback);
			GenData.Add (OnCanceled);
			GenData.Add (CanPlace);

			if (ExtraData != null) { // add extra data if is not null
				for (int i = 0; i < ExtraData.Length; i++) {
					GenData.Add (ExtraData [i]);
				}
			}

			// add init settings
			GenData.Add (InitSnapType);
			Statemachine.changeState (new PlacePlacableObject(),GenData,Interrupt);
		}

		public void PlaceModularSet(ModularSet ModularSet,Delegates.GenericDataCallbak PlacedCallback,System.Action<object[]> OnCanceled,System.Func<ModularPlacableObject,bool> CanPlace,object[] ExtraData = default(object[]),bool Interrupt = false){
			PlaceModularPlacable ((ModularPlacableObject)ModularSet,PlacedCallback,CanPlace,OnCanceled,SnapTypes.Default,ExtraData,Interrupt);
		}

		// private voids
		public ModularSet InitModularSet(ModularSetData SetData,bool InitInEditMode = false){
			return InitModularSet (SetData,SetData.Transform.LocalPosition,Quaternion.Euler(SetData.Transform.LocalEulerAngles),SetData.Transform.LocalScale,InitInEditMode);
		}
		public ModularSet InitModularSet(ModularSetData SetData,Vector3 InitPosition,Quaternion InitRotation, Vector3 InitScale,bool InitInEditMode = false){
			return InitModularSetCore (InitNewModularSet (InitPosition,InitRotation,InitScale),SetData,InitInEditMode);
		}
		public ModularSet InitModularSet(ModularSetData SetData, ModularSet ReferenceSet,bool InitInEditMode = false){
			return InitModularSetCore (InitNewModularSet(ReferenceSet),SetData,InitInEditMode);
		}
		private ModularSet InitModularSetCore(ModularSet NewSet,ModularSetData SetData,bool InitInEditMode = false){
			if (InitInEditMode) {NewSet.InitializeEditable ();} // init in edit mode
			NewSet.AsyncLoadModularSet(SetData,(object[] Data) =>{
				ModularSet Set = (ModularSet)Data[0]; // get set data
				ModularPieceSaveData PieceData = (ModularPieceSaveData)Data[1]; // get piece data

				ModularPiece Piece = InitStoreItem(PieceData); // init store item to modular piece
				if(Piece != null){ // only add when piece is not null
					Set.AddModularPiece(Piece,(ModularPlacableData)PieceData); // add store item
				}
			});
			return NewSet;
		} // core function for init modular set

		public bool StrokeModularPlacable(ModularPieceData PieceData,System.Action<ModularPlacableObject> OnPlaced,System.Func<int,bool> CanPlace){
			if (PieceData != null) {
				// init new modular set
				GameManager.I.Modular.ScopedSet = InitNewModularSet ();
				GameManager.I.Modular.ScopedSet.InitializeEditable ();

				// place
				HashSet<ModularSet> AffectedSets = new HashSet<ModularSet> ();
				if (PieceData.Prefab != null && PieceData.Prefab.GetComponent<StrokeModularPiece> () != null) {
					StrokeModularPiece StrokePiece = PieceData.Prefab.GetComponent<StrokeModularPiece> (); // get stroke piece from prefab
					ModularStrokeSetData StrokeSet = GameManager.I.Modular.FindStrokeSet(PieceData.Key);
					if (StrokeSet != null) { // if there is found a strokeset
						return StrokeModularPlacable(PieceData,(ModularPlacableObject Placable)=>{ // on deleted
							if(typeof(ModularPiece).IsAssignableFrom(Placable.GetType())){
								ModularPiece Piece = (ModularPiece)Placable;
								ModularSet ParentSet = Piece.ParentSet;
								if(ParentSet != null){
									bool NotDestoryed = ParentSet.RemoveModularPiece(Piece); // remove piece from set
									if(!AffectedSets.Contains(ParentSet)){ // add set to affected sets
									AffectedSets.Add(ParentSet);} else if(!NotDestoryed){
										AffectedSets.Remove(ParentSet);
									}
								}
							}
						},OnPlaced,(object[] Data)=>{ // on cancel
							if(!AffectedSets.Contains(GameManager.I.Modular.ScopedSet)){AffectedSets.Add(GameManager.I.Modular.ScopedSet);}
							foreach(ModularSet Set in AffectedSets){
								Set.FinalizeSet(Management.GameManager.I.Modular.ModularPieceLayer);
							}// Update render sets of affected modular sets
							Statemachine.changeState (new ModularSpectate(),this);
						},(StrokeType Type)=>{ // return information
							return StrokeSet.FindStrokePieceComponent(Type);
						},(StrokeType Type)=>{ // return new instance
							ModularPieceData Piece = StrokeSet.FindPiece(Type);
							if(Piece == null){Piece = PieceData;}
							var Object = InitStoreItem(Piece,Vector3.one);
							Object.InitializeEditable();
							GameManager.I.Modular.ScopedSet.AddModularPiece(Object); // add piece to set
							return Object;
						},CanPlace);
					}
				}

			}
			return false;
		}
		public bool StrokeModularPlacable(ModularPieceData PieceData,System.Action<ModularPlacableObject> OnDeleted,System.Action<ModularPlacableObject> OnPlaced,System.Action<object[]> OnCancel,System.Func<StrokeType,ModularPlacableObject> InfoGetter, System.Func<StrokeType,ModularPlacableObject> InstanceGetter,System.Func<int,bool> CanPlace){
			List<object> GenData = new List<object> ();
			GenData.Add (this);
			GenData.Add (OnCancel);
			GenData.Add (new System.Func<StrokeType,ModularPlacableObject>[]{
				InfoGetter,
				InstanceGetter
			});
			GenData.Add (CanPlace);
			GenData.Add (new System.Action<ModularPlacableObject>[]{
				OnPlaced,
				OnDeleted
			});
			GenData.Add (GameManager.I.Modular.FindStrokeSet(PieceData.Key));

			Statemachine.changeState (new StrokePlacableObject(),GenData); // change to stroke placable object
			return true;
		} 
		public bool StrokeModularPlacable(string PieceKey,System.Action<ModularPlacableObject> OnPlaced,System.Func<int,bool> CanPlace){
			return StrokeModularPlacable(GameManager.I.Modular.FindPiece (PieceKey),OnPlaced,CanPlace);
		}
		public bool StrokeModularPlacable(StrokeModularPiece Piece,ModularStrokeSetData SetData){
			List<object> GenData = new List<object> ();
			GenData.Add (this);
			GenData.Add (Piece);
			GenData.Add (SetData);

			Statemachine.changeState (new StrokePlacableObject(),GenData); // change to stroke placable object
			return true;
		}

		public ModularSet InitNewModularSet(){
			ModularSet ModularSet = new GameObject ("Blueprint",typeof(ModularSet)).GetComponent<ModularSet> ();
			return ModularSet; // create new set and scope
		}
		public ModularSet InitNewModularSet(Vector3 InitPosition, Quaternion InitRotation, Vector3 InitScale){
			ModularSet NewSet = InitNewModularSet (); // init new set
			NewSet.transform.localPosition = InitPosition;
			NewSet.transform.localScale = InitScale;
			NewSet.NoneOffsettedRotation = InitRotation;
			return NewSet;
		}
		public ModularSet InitNewModularSet(ModularSet ValueReference){
			ModularSet NewSet = InitNewModularSet (); // init new set
			NewSet.Position = ValueReference.Position;
			NewSet.RotationOffset = ValueReference.RotationOffset;
			NewSet.NoneOffsettedRotation = ValueReference.NoneOffsettedRotation;
			NewSet.transform.rotation = ValueReference.transform.rotation;
			NewSet.transform.localScale = ValueReference.transform.localScale;
			return NewSet;
		}

		public ModularPiece InitStoreItem(ModularPieceSaveData PieceSaveData){
			ModularPieceData PieceData = GameManager.I.Modular.FindPiece(PieceSaveData.ID); // find modular piece

			if (PieceData != null) {
				ModularPiece piece = InitStoreItem (PieceData,PieceSaveData.LocalScale,Vector3.zero,PieceSaveData.LocalEulerAngles,PieceSaveData.LocalPosition);
				piece.InitializeTextureData(PieceSaveData.TextureDataKeys);
				piece.InitializeColors (PieceSaveData.Colors); // initialize colors
				return piece;
			}
			return null;
		}
		public ModularPiece InitStoreItem(ModularPieceData Piece,Vector3 Scale,Vector3 RotationOffset = default(Vector3),Vector3 InitRotation = default(Vector3),Vector3 InitPosition = default(Vector3)){
			if (Piece != null) {
				GameObject obj = Piece.Prefab; // pick base object

				// instantiate item
				ModularPiece piece = GameObject.Instantiate (obj, InitPosition, Quaternion.identity).GetComponent<ModularPiece> ();
				piece.gameObject.name = Piece.Name;
				piece.InitializeID (Piece.Key);
				piece.InitializeTextureData ();
				piece.InitializeColors ();
				piece.transform.localScale = Scale;
				piece.transform.localPosition = InitPosition;
				piece.NoneOffsettedRotation = Quaternion.Euler (InitRotation); // set rotation with rotation property
				piece.RotationOffset = Quaternion.Euler (RotationOffset); // set rotation offset
				piece.Initialize (GameManager.I.Modular.ScopedSet); // initialize piece data
				return piece;
			}
			return null;

		}
	}
}
