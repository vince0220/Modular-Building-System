using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using DreamInCode.BuildingSystem;
using Management;
using Modular;
using System.Linq;

namespace Modular.States{
	public class SelectPlacableObject : IControllableState {
		#region Variables
		// base variables
		private ModularBuildingSystem System;
		private ModularPlacableObject[] ScopedObjects;
		private List<object> GenData;
		private System.Func<ModularPlacableObject,bool> ContentFilter;
		private System.Func<ModularPlacableObject,UI.Toolbar.CustomView[]> CustomContentFilter;

		// Temp variables
		private GameObject LocalMover;
		private List<ParentCache> ObjectParentCache = new List<ParentCache>();
		private SceneHandle PositionHandle;
		private SceneHandle ScaleHandle;
		private SceneHandle RotationHandle;
		private bool LocalSpace = true;
		private Vector3 ScreenOffset;
		private UI.Toolbar.Toolbar SettingsBar;
		private Vector3 LastPosition;

		// restriction cache
		private PositionRestrictions _PositionRestriction = PositionRestrictions.None;
		private bool _Scalable = true;
		private float _PositionGridRestriction = 0;
		private float _RotationStepRestriction = 0;
		private SpaceRestrictions _SpaceRestriction = SpaceRestrictions.None;
		private RotationRestrictions _RotationRestriction = RotationRestrictions.XYZ;
		private bool _PerObjectTransform;
		#endregion

		#region IState implementation
		public void Enter (List<object> GenData = null)
		{
			this.GenData = GenData;
			this.System = GenData.Find<ModularBuildingSystem> (); // set base system
			this.ScopedObjects = GenData.Find<ModularPlacableObject[]>(); // set scoped pieces
			this.ContentFilter = GenData.Find<System.Func<ModularPlacableObject,bool>>();
			this.CustomContentFilter = GenData.Find<System.Func<ModularPlacableObject,UI.Toolbar.CustomView[]>>();

			// update default restrictions
			for (int i = 0; i < ScopedObjects.Length; i++) { // go through all pieces
				UpdatePieceRestrictions((ModularPlacableObject)ScopedObjects[i]); // update modular piece restrictions
			}
			LocalSpace = (_SpaceRestriction != SpaceRestrictions.None) ? false : true; // local space check

			// final init
			Init (); // init state
		}

		public void Update(){
			
		}
		public void ForceExit ()
		{
			ExitFunctionality (false);
		}
		public void Exit ()
		{
			ExitFunctionality(false);
		}
		public void ReEnterExit(){
			ExitFunctionality (true);
		}
		public void Interrupt(){
			ExitFunctionality (false);
		}
		#endregion

		#region Init voids
		private void Init(){
			// set settings
			InitLocalMove (); // init local mover
			InitUI(); // init UI
			InitSceneUI(); // init the scene UI. For instance 3d handles
			InitKeys(); // init keys and callbacks

			for (int i = 0; i < ScopedObjects.Length; i++) {
				ScopedObjects [i].RemoveFromGrid (); // remove scoped objects from grid
				ScopedObjects [i].SetCollidersActive (false,true);
			}

			EnablePositionHandle (); // enable position as default
		}
		private void InitLocalMove(){
			LocalMover = new GameObject ("LocalMover");
			if (ScopedObjects.Length == 1) { // set rotation of mover if only has 1 piece
				LocalMover.transform.rotation = ScopedObjects [0].transform.rotation;
			}
			LocalMover.transform.position = ScopedObjects.Center(); // get center of scoped objects

			// init parent cache
			for (int i = 0; i < ScopedObjects.Length; i++) {
				ScopedObjects [i].OnDeplaced (); // call deplaced function
				ObjectParentCache.Add (
					new ParentCache{
						PlacableObject = ScopedObjects[i],
						ParentTrans = ScopedObjects[i].transform.parent
					}.ReparentTo(LocalMover.transform)
				);
			} // create new parent cache and reparent to local mover
		}
		private void UpdateLocalMoverPosition(){
			// Unparent
			for (int i = 0; i < ObjectParentCache.Count; i++) {
				ObjectParentCache [i].ReparentTo (null);
			}

			// update position
			LocalMover.transform.position = ScopedObjects.Center(); // get center of scoped objects

			// update rotation
			if (ScopedObjects.Length == 1) { // set rotation of mover if only has 1 piece
				LocalMover.transform.rotation = ScopedObjects [0].transform.rotation;
			}
				
			// Unparent
			for (int i = 0; i < ObjectParentCache.Count; i++) {
				ObjectParentCache [i].ReparentTo (LocalMover.transform);
			}

			UpdatePositionTransfrom(); // update the position handle
			UpdateRotationTransform (); // update the rotation handle
			UpdateScaleTransfrom (); // update the scale handle
		}
		private void InitUI(){
			#region Content
			List<UI.Toolbar.CustomView> ContentViews = new List<UI.Toolbar.CustomView>();
			if(ContentFilter != null){
				for(int i = 0; i < ScopedObjects.Length; i++){
					if(ContentFilter(ScopedObjects[i])){
						ContentViews.Add(new UI.Toolbar.CustomView(ScopedObjects[i]));
					}
				}
			}
			if(CustomContentFilter != null){
				for(int i = 0; i < ScopedObjects.Length; i++){
					ContentViews.AddRange(CustomContentFilter(ScopedObjects[i])); // add custom content to content views
				}
			}
			#endregion

			// init toolbar
			UI.Toolbar.ToolbarSettings BarSettings = new UI.Toolbar.ToolbarSettings {
				HeaderSections = InitializeHeaderSections(),
				ToolbarIconSize = 28f,
				Title = (ScopedObjects.Length == 1) ? ScopedObjects [0].gameObject.name : ScopedObjects.Length + " Objects Selected",
			};

			// Add content
			if (ContentViews.Count > 0) {BarSettings.Content = ContentViews.ToArray ();}

			SettingsBar = GameManager.I.UI.InitToolbar (BarSettings, ScopedObjects);
		}
		private UI.Toolbar.HeaderAction InitColorSwatch(ModularPlacableObject.ColorGroup[] ColorGroups, int ColorIndex){
			return new UI.Toolbar.HeaderAction(ColorGroups[ColorIndex].Color,(object[] Data)=>{
				ColorGroups[ColorIndex].Color = Data.Find<Color>();
			});
		}
		private UI.Toolbar.HeaderAction InitTexturePicker(ModularPlacableObject.TextureGroup TextureGroup){
			return new UI.Toolbar.HeaderAction(TextureGroup.Texture,TextureGroup.TextureArray,(object[] Data)=>{
				TextureGroup.Texture = Data.Find<ModularTextureData>();
				SettingsBar.UpdateSection("ColorGroup",GetToolbarColorSection());
			},UI.ToolTipComponent.AlignSide.Top);
		}
		private UI.Toolbar.HeaderSection[] InitializeHeaderSections(){
			List<UI.Toolbar.HeaderAction> MovementHeaderActions = new List<UI.Toolbar.HeaderAction> ();
			List<UI.Toolbar.HeaderAction> Toggles = new List<UI.Toolbar.HeaderAction> ();
			List<UI.Toolbar.HeaderAction> CustomActions = new List<UI.Toolbar.HeaderAction> ();
			List<UI.Toolbar.HeaderAction> EndActions = new List<UI.Toolbar.HeaderAction> ();
			List<UI.Toolbar.HeaderAction> TextureActions = new List<UI.Toolbar.HeaderAction> ();

			// add actions
			#region Movement actions
			MovementHeaderActions.Add(new UI.Toolbar.HeaderAction(UI.Toolbar.HeaderActionType.Normal,new IconPack.IconType[]{IconPack.IconType.Move},new string[]{"Move"},new System.Action<object[]>[]{ // move action
				// callbacks for move action
				OnMoveAction
			}));
			MovementHeaderActions.Add (new UI.Toolbar.HeaderAction(UI.Toolbar.HeaderActionType.Normal,new IconPack.IconType[]{IconPack.IconType.Rotate},new string[]{"Rotate"},new System.Action<object[]>[]{ // Rotate action
				// callbacks for move action
				OnRotateAction
			}));

			if (_Scalable) { // if can scale add scale function
				MovementHeaderActions.Add (new UI.Toolbar.HeaderAction (UI.Toolbar.HeaderActionType.Normal, new IconPack.IconType[]{IconPack.IconType.Scale}, new string[]{ "Scale" }, new System.Action<object[]>[] { // Scale action
					// callbacks for move action
					OnScaleAction
				}));
			}
			#endregion
			#region Toggle actions
			if(_SpaceRestriction == SpaceRestrictions.None){
				Toggles.Add(new UI.Toolbar.HeaderAction(UI.Toolbar.HeaderActionType.Toggle,new IconPack.IconType[]{IconPack.IconType.LocalSpace,IconPack.IconType.WorldSpace},new string[]{"Local space","World space"},new System.Action<object[]>[]{
					// callbacks
					(object[] Data) => {SetLocalSpace(true);}, // set local space
					(object[] Data) => {SetLocalSpace(false);} // set world space
				}));
			}
			Toggles.Add(new UI.Toolbar.HeaderAction(UI.Toolbar.HeaderActionType.Toggle,new IconPack.IconType[]{IconPack.IconType.GlobalTransform,IconPack.IconType.PerObjectTransform},new string[]{"All objects","Per object"},new System.Action<object[]>[]{
				// callbacks
				(object[] Data) => {_PerObjectTransform = false;}, // set all objects
				(object[] Data) => {_PerObjectTransform = true;} // set per objects
			}));
			#endregion
			#region Custom actions
			UI.Toolbar.HeaderAction[] FoundCustomActions = GenData.Find<UI.Toolbar.HeaderAction[]>();
			if(FoundCustomActions != null){
				CustomActions = FoundCustomActions.ToList(); // set custom header actions
			}
			#endregion
			#region End actions
			if(ScopedObjects.Length == 1){
				if(ScopedObjects[0].Editable){ //if only one editable object is selected
					EndActions.Add(new UI.Toolbar.HeaderAction(UI.Toolbar.HeaderActionType.Normal,new IconPack.IconType[]{IconPack.IconType.EditDesign},new string[]{"Edit object"},new System.Action<object[]>[]{ // move action
						// callbacks for move action
						OnEditAction
					}));
				}

				EndActions.Add(new UI.Toolbar.HeaderAction(UI.Toolbar.HeaderActionType.Normal,new IconPack.IconType[]{IconPack.IconType.ReplaceObject},new string[]{"Replace object"},new System.Action<object[]>[]{ // move action
					// callbacks for move action
					OnReplaceObject
				}));
			}
			EndActions.Add(new UI.Toolbar.HeaderAction(UI.Toolbar.HeaderActionType.Normal,new IconPack.IconType[]{IconPack.IconType.Duplicate},new string[]{"Duplicate selected"}, new System.Action<object[]>[]{
				// callback for duplicating selected item
				OnDuplicate
			}));
			EndActions.Add(new UI.Toolbar.HeaderAction(UI.Toolbar.HeaderActionType.Normal,new IconPack.IconType[]{IconPack.IconType.Delete},new string[]{"Delete selected"},new System.Action<object[]>[]{ // move action
				// callbacks for move action
				OnDelete
			}));
			#endregion

			#region Textures
			ModularPlacableObject.TextureGroup[] TextureGroups = ScopedObjects.TextureGroups();
			if(TextureGroups.Length < 8){ // limit colors to 8
				for(int i = 0; i < TextureGroups.Length; i++){
					TextureActions.Add(InitTexturePicker(TextureGroups[i]));
				}
			}
			#endregion

			// init toolbar
			return new UI.Toolbar.HeaderSection[] { // movement actions section
				new UI.Toolbar.HeaderSection (MovementHeaderActions.ToArray ()),
				// toggles
				new UI.Toolbar.HeaderSection (Toggles.ToArray ()),

				new UI.Toolbar.HeaderSection (CustomActions.ToArray ()),

				// end actions
				new UI.Toolbar.HeaderSection (EndActions.ToArray ()),

				// color actions
				GetToolbarColorSection(),

				// Texture Actions
				new UI.Toolbar.HeaderSection (TextureActions.ToArray ())
			};
		}
		private UI.Toolbar.HeaderSection GetToolbarColorSection(){
			List<UI.Toolbar.HeaderAction> ColorActions = new List<UI.Toolbar.HeaderAction> ();
			#region Colors
			ModularPlacableObject.ColorGroup[] ColorGroups = ScopedObjects.ColorGroups();
			if(ColorGroups.Length < 8){ // limit colors to 8
				for(int i = 0; i < ColorGroups.Length; i++){
					ColorActions.Add(InitColorSwatch(ColorGroups,i));
				}
			}
			#endregion
			return new UI.Toolbar.HeaderSection (ColorActions.ToArray (),"ColorGroup");
		}
		private void InitSceneUI(){
			// init position handle
			PositionHandle = GameObject.Instantiate (GameManager.I.Modular.PositionHandles, LocalMover.transform.position, Quaternion.identity).GetComponent<SceneHandle>();
			PositionHandle.UpdateLocalScale ();
			PositionHandle.OnDragAction (0,OnPositionHandleDragZ); // Z axis
			PositionHandle.OnDragAction (1,OnPositionHandleDragY); // Y axis
			PositionHandle.OnDragAction (2,OnPositionHandleDragX); // X axis
			PositionHandle.OnGlobalMouseDown(OnPositionMouseDown); // Global on mouse down callback
			PositionHandle.OnGlobalMouseUp(OnPositionMouseUp);
			PositionHandle.gameObject.SetActive (false);
			UpdatePositionTransfrom ();

			// init Rotation Handle
			RotationHandle = GameObject.Instantiate (GameManager.I.Modular.RotationHandles, LocalMover.transform.position, Quaternion.identity).GetComponent<SceneHandle>();
			RotationHandle.UpdateLocalScale ();
			RotationHandle.OnDragAction (0,OnRotationY); // Y axis
			RotationHandle.OnDragAction (1,OnRotationZ); // Z axis
			RotationHandle.OnDragAction (2,OnRotationX); // X axis
			RotationHandle.OnGlobalMouseDown(OnRotationMouseDown);
			RotationHandle.OnGlobalMouseUp (OnRotationGlobalMouseUp);
			RotationHandle.gameObject.SetActive (false);
			// set handle restrictions
			switch (_RotationRestriction) {
				case RotationRestrictions.Y:
					RotationHandle.SetHandleVisiblity(1,true);
					RotationHandle.SetHandleVisiblity (2,true);
					break;
			}
			UpdateRotationTransform ();

			// init scale Handle
			ScaleHandle = GameObject.Instantiate(GameManager.I.Modular.ScaleHandles,LocalMover.transform.position,Quaternion.identity).GetComponent<SceneHandle>();
			ScaleHandle.UpdateLocalScale ();
			ScaleHandle.OnDragAction (0,OnScaleZ);
			ScaleHandle.OnDragAction (1,OnScaleY);
			ScaleHandle.OnDragAction (2,OnScaleX);
			ScaleHandle.OnDragAction (3,OnScaleAll);
			ScaleHandle.OnGlobalMouseDown (OnScaleMouseDown);
			ScaleHandle.OnGlobalMouseUp (OnScaleMouseUp);
			ScaleHandle.gameObject.SetActive (false);
		}
		private void InitKeys(){
			GameManager.I.Utils.RegisterOnChangedCallback (OnUndoChanged); // register on undo change
			GameManager.I.Select.AddEmptyClick (OnOutsideClick); // add on outside click
			GameManager.I.Keys.AddOnDownCallback(GameManager.I.Constants.C_DELETE,OnDeleteKey);
		}
		#endregion

		#region Functions
		private void ExitFunctionality(bool ReEnter){
			// remove UI
			GameManager.I.Select.RemoveEmptyClickCallback(OnOutsideClick); // empty click
			GameManager.I.UI.DestroyToolbar(SettingsBar); // Destoy all UI toolbars
			GameManager.I.Keys.RemoveOnDownCallback(GameManager.I.Constants.C_DELETE,OnDeleteKey); // remove delete callback
			GameManager.I.Utils.ClearOnChangedCallbacks(); // clear on changed callbacks
			if(!ReEnter){ // if re enter dont deselect all
				GameManager.I.Select.DeselectAll(true); // deselect all
			}

			// destory Handles
			PositionHandle.Destoy();
			RotationHandle.Destoy ();
			ScaleHandle.Destoy ();

			// reset parents
			ResetParents();

			for (int i = 0; i < ScopedObjects.Length; i++) {
				ScopedObjects [i].UpdateTransform (); // update trans
				ScopedObjects [i].SetCollidersActive(true,true);
				ScopedObjects [i].OnPlaced(); // call on placed callback
			}

			// destoy Local Mover
			GameObject.Destroy (LocalMover);
		}
		private void ResetParents(){
			for (int i = 0; i < ObjectParentCache.Count; i++) {ObjectParentCache [i].ResetParent ();}// reset parent
		}
		private void OnOutsideClick(){
			System.Statemachine.changeState(new ModularSpectate(),System);
		}
		private void SetObjectScreenOffset(Vector3 StartPosition){
			ScreenOffset = StartPosition - Camera.main.ScreenToWorldPoint (
				new Vector3(Input.mousePosition.x,
					Input.mousePosition.y,
					Camera.main.WorldToScreenPoint(StartPosition).z
				)
			);
		}
		private Vector3 WorldMousePosition {
			get {
				Vector3 CurScreenPoint = new Vector3 (Input.mousePosition.x, Input.mousePosition.y, Camera.main.WorldToScreenPoint (PositionHandle.transform.position).z);
				return Camera.main.ScreenToWorldPoint (CurScreenPoint) + ScreenOffset;
			}
		}
		private void UpdatePieceRestrictions(ModularPlacableObject Placable){
			_PositionRestriction = ((int)_PositionRestriction < (int)Placable.PositionRestriction)?Placable.PositionRestriction:_PositionRestriction;// position restrictions
			_Scalable = (!Placable.Scalable)?Placable.Scalable:_Scalable; // scaling restriction
			_RotationStepRestriction = (Placable.RotationStepRestriction > _RotationStepRestriction)?Placable.RotationStepRestriction:_RotationStepRestriction; // rotation step restrictions
			_RotationRestriction = ((int)_RotationRestriction < (int)Placable.RotationRestriction)?Placable.RotationRestriction:_RotationRestriction;// Rotation restrictions
			_SpaceRestriction = ((int)_SpaceRestriction < (int)Placable.SpaceRestriction)?Placable.SpaceRestriction:_SpaceRestriction;// Space restrictions

			if (_PositionRestriction != PositionRestrictions.None) {
				_PositionGridRestriction = (_PositionGridRestriction < Placable.PositionGridRestriction) ? Placable.PositionGridRestriction : _PositionGridRestriction; // set position grid restriction
			}
		}
		#endregion

		#region Toolbar Actions
		private void OnDuplicate(object[] Data){
			List<GameObject> DuplicatedObjects = new List<GameObject> ();
			List<LayerMask> CustomMasks = new List<LayerMask> ();

			bool CanDuplicate = true;
			string Message = "";
			for (int i = 0; i < ScopedObjects.Length; i++) {
				if (!ScopedObjects [i].CanDuplicate (out Message)) {
					CanDuplicate = false;
					break;
				}
			}

			if (CanDuplicate) {
				for (int i = 0; i < ScopedObjects.Length; i++) {
					ModularPlacableObject Duplicate = ScopedObjects [i].Duplicate ();
					Duplicate.transform.localScale = Vector3.Scale (Duplicate.transform.localScale, LocalMover.transform.localScale);
					DuplicatedObjects.Add (Duplicate.gameObject); // Duplicate item
					CustomMasks.Add (Duplicate.SelectionLayerMask); // add custom selection layermask

					// remove money and spawn indicator
					int Price = GameManager.I.Economy.EvaluateModularPlacable(ScopedObjects [i]);
					GameManager.I.Economy.RemoveMoney(Price);
					GameManager.I.Economy.SpawnMoneyIndicator(ScopedObjects [i].Position,Price);
				}
				GameManager.I.Select.SelectObjects (DuplicatedObjects.ToArray (), CustomMasks.ToArray ()); // select new duplicated items
			} else {
				GameManager.I.UI.Toast (Message);
			}
		}
		private void OnUndoChanged(){
			UpdateLocalMoverPosition (); // update local mover
		}
		private void OnEditAction(object[] Data){
			GameManager.I.Select.DeselectAll (); // deselect all selected
			GameManager.I.Modular.EditModularSet (ScopedObjects[0]); // edit first modular set

			System.Statemachine.changeState(new ModularSpectate(),System);
		}
		private void OnRotateAction(object[] Data){
			EnableRotation ();
		}
		private void OnMoveAction(object[] Data){
			EnablePositionHandle ();
		}
		private void OnScaleAction(object[] Data){
			EnableScaleHandle ();
		}
		private void ToggleLocal(object[] Data){
			LocalSpace = !LocalSpace; // toggle local space

			SetLocalSpace (LocalSpace);
		}
		private void SetLocalSpace(bool Local){
			LocalSpace = Local;
			// Update all handles
			UpdatePositionTransfrom(); // update the position handle
			UpdateRotationTransform (); // update the rotation handle
			UpdateScaleTransfrom (); // update the scale handle
		}

		private void OnReplaceObject(object[] Data){
			if (ScopedObjects.Length > 0) {
				System.ReplaceModularPlacable (ScopedObjects [0]);
			}
		}
		#endregion

		#region Functions
		#region Delete
		private void OnDeleteKey(){
			OnDelete (new object[]{});
		}
		private void OnDelete(object[] Data){
			GameManager.I.Select.DeselectAll (); // deselect all objects

			// Delete the selection modular placable objects
			for (int i = 0; i < ScopedObjects.Length; i++) {
				ScopedObjects [i].DestoyUndo (); // destoy scoped object by calling inner destroy function
			}

			// Empty stored values
			ResetParents(); // reset objects parents
			ScopedObjects = new ModularPlacableObject[]{}; // override scoped objects array to empty one to avoid null pointers in any case

			// Change state
			System.Statemachine.changeState(new ModularSpectate(),System);
		}
		#endregion
		#region Rotation Handle Functions
		// variables
		private float baseAngle;
		private Quaternion BaseRotation;
		private Quaternion TempRotation;

		// functions
		private void RotateOnAxis(Vector3 Axis){
			Vector3 Direction = Axis; // set base axis
			if (LocalSpace) {
				Direction = BaseRotation * Axis; // localize rotaion
			}

			var dir = Camera.main.WorldToScreenPoint(LocalMover.transform.position);
			dir = Input.mousePosition - dir;
			var angle =  Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - baseAngle;

			float side = Vector3.Dot (Direction, (Camera.main.transform.position - LocalMover.transform.position));
			if (side < 0) {
				TempRotation = Quaternion.AngleAxis(angle,Direction) * BaseRotation;
			} else {
				TempRotation = Quaternion.AngleAxis(-angle,Direction) * BaseRotation;
			}

			// snap rotation
			if (_RotationStepRestriction > 0) {
				LocalMover.transform.rotation = LocalMover.transform.rotation.SnapRotation (_RotationStepRestriction, TempRotation, Quaternion.LookRotation (LocalForward));
			} else {
				LocalMover.transform.rotation = TempRotation;
			}
			
			UpdateRotationTransform ();
		}

		// actions
		private void OnRotationX(){
			RotateOnAxis (Vector3.right);
		}
		private void OnRotationY(){
			RotateOnAxis (Vector3.up);
		}
		private void OnRotationZ(){
			RotateOnAxis (Vector3.forward);
		}

		// callbacks
		private void OnRotationMouseDown(){
			var dir = Camera.main.WorldToScreenPoint(LocalMover.transform.position);
			dir = Input.mousePosition - dir;
			baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
			baseAngle -= Mathf.Atan2(Vector3.right.y, Vector3.right.x) * Mathf.Rad2Deg;
			BaseRotation = LocalMover.transform.rotation;
		}
		private void OnRotationGlobalMouseUp(){
			ScopedObjects.RegisterUndoChange (); // register changes
		}
		private void EnableRotation(){
			PositionHandle.gameObject.SetActive (false);
			ScaleHandle.gameObject.SetActive (false);
			RotationHandle.gameObject.SetActive (true);
			UpdateRotationTransform ();
		}
		private void UpdateRotationTransform(){
			// update position
			RotationHandle.UpdateLocalScale();
			RotationHandle.transform.position = LocalMover.transform.position;

			if (LocalSpace) {
				RotationHandle.transform.rotation = LocalMover.transform.rotation;
			} else {
				RotationHandle.transform.rotation = Quaternion.identity;
			}
		}
		#endregion
		#region Position Handle functions
		// variables
		private Vector3 StartPosition;
		private Vector3 TempPosition;
		private Vector3 GridTempPosition;

		// actions axis
		private void Position(Vector3 IdentityAxis){
			Quaternion Rotation = (LocalSpace) ? LocalMover.transform.rotation : Quaternion.identity;
			Vector3 Forward = LocalMover.transform.forward;

			Vector3 Centerd = WorldMousePosition - StartPosition;
			Vector3 WorldOffset = Vector3.Scale(Centerd.ToWorldRotationFull (Rotation, Vector3.zero),IdentityAxis); // world offset
			Vector3 LocalOffset = WorldOffset.ToLocalRotationFull(Rotation,Vector3.zero);
			
			TempPosition = StartPosition + LocalOffset; // set temp position

			// round to restricted grid position
			GridTempPosition = (_PositionGridRestriction > 0)?GlobalCalulations.RoundVectorLocal
				(_PositionGridRestriction,TempPosition,LocalForward,StartPosition):TempPosition;

			OnPositionChanged(); // update position
		}

		private void OnPositionHandleDragX(){
			Position (Vector3.right);
		}
		private void OnPositionHandleDragY(){
			Position (Vector3.up);
		}
		private void OnPositionHandleDragZ(){
			Position (Vector3.forward);
		}

		// actions start/end
		private void OnPositionMouseDown(){
			StartPosition = PositionHandle.transform.position;
			SetObjectScreenOffset (LocalMover.transform.position); // calculate offset
		}
		private void OnPositionMouseUp(){
			ScopedObjects.RegisterUndoChange (); // register undo
		}

		// functions
		private void OnPositionChanged(){
			LocalMover.transform.position = GridTempPosition;
			PositionHandle.transform.position = GridTempPosition;
			if (LastPosition != GridTempPosition) {
				for (int i = 0; i < ScopedObjects.Length; i++) {
					ScopedObjects [i].OnPositionChanged (); // Pos has changed
				}
				LastPosition = GridTempPosition;
			}
		}
		private void EnablePositionHandle(){
			PositionHandle.gameObject.SetActive (true);
			ScaleHandle.gameObject.SetActive (false);
			RotationHandle.gameObject.SetActive (false);
			UpdatePositionTransfrom ();
		}
		private void UpdatePositionTransfrom(){
			PositionHandle.UpdateLocalScale ();
			if (LocalSpace) {
				PositionHandle.transform.rotation = LocalMover.transform.rotation;
			} else {
				PositionHandle.transform.rotation = Quaternion.identity;
			}
			PositionHandle.transform.position = LocalMover.transform.position;
		}
		#endregion
		#region Scale Handle Functions
		// variables
		private Vector3[] StartScale;
		private Vector3 LocalMoverStartScale;
		private float ScaleSpeed = 2;

		// actions
		private void Scale(Vector3 Axis){
			if (_PerObjectTransform) {
				for (int i = 0; i < ScopedObjects.Length; i++) {
					ScopedObjects [i].transform.localScale = NewScale (StartScale [i], ScopedObjects [i].transform.rotation * Axis);
				}
			} else {
				LocalMover.transform.localScale = NewScale (LocalMoverStartScale,LocalMover.transform.rotation * Axis);
			}
		}
		private Vector3 NewScale(Vector3 Start,Vector3 Axis){
			// calculate mouse input
			Vector3 Pos = WorldMousePosition;
			float Distance = Vector3.Distance (LocalMover.transform.position,Pos);
			float dot = Vector3.Dot ((Pos - LocalMover.transform.position).normalized,LocalMover.transform.rotation * Axis);
			Distance *= dot;

			return Start + (Axis * (Distance * ScaleSpeed));
		}
		private void OnScaleX(){
			Scale (Vector3.right);
		}
		private void OnScaleY(){
			Scale (Vector3.up);
		}
		private void OnScaleZ(){
			Scale (Vector3.forward);
		}
		private void OnScaleAll(){
			// calculate mouse input
			Vector2 MoveSpeed = new Vector2 (Input.GetAxis("Mouse X"),Input.GetAxis("Mouse Y"));
			var dir = Camera.main.WorldToScreenPoint(LocalMover.transform.position);
			dir = Input.mousePosition - dir;
			float dot = Vector3.Dot(dir,Vector3.up);

			if (_PerObjectTransform) { // scale per object
				// For each object
				for (int i = 0; i < ScopedObjects.Length; i++) {
					ScopedObjects [i].transform.localScale = StartScale [i] + Vector3.Scale (StartScale [i], Vector3.one * dot / 100);
				}
			} else {
				LocalMover.transform.localScale = LocalMoverStartScale + Vector3.Scale (LocalMoverStartScale,Vector3.one * dot / 100); // scale in total
			}
		}

		// functions
		private void OnScaleMouseUp(){
			ScopedObjects.RegisterUndoChange (); // register undo
		}
		private void OnScaleMouseDown(){
			LocalMoverStartScale = LocalMover.transform.localScale;
			StartScale = new Vector3[ScopedObjects.Length];
			for (int i = 0; i < StartScale.Length; i++) {
				StartScale [i] = ScopedObjects [i].transform.localScale;
			}
			SetObjectScreenOffset (LocalMover.transform.position);
		}
		private void EnableScaleHandle(){
			PositionHandle.gameObject.SetActive (false);
			ScaleHandle.gameObject.SetActive (true);
			RotationHandle.gameObject.SetActive (false);
			UpdateScaleTransfrom ();
		}
		private void UpdateScaleTransfrom(){
			// update position
			ScaleHandle.UpdateLocalScale();
			ScaleHandle.transform.position = LocalMover.transform.position;
			ScaleHandle.transform.rotation = LocalMover.transform.rotation;
		}
		#endregion
		#region Multiselect Functions
		private void CheckMultiselectDragging(){

		}
		#endregion
		#endregion

		#region private getters
		private Vector3 LocalForward{
			get{
				return (_SpaceRestriction == SpaceRestrictions.WorldOnly) ? Vector3.forward : LocalMover.transform.forward;
			}
		}
		#endregion

		#region Structs
		public struct ParentCache{
			public ModularPlacableObject PlacableObject;
			public Transform ParentTrans;

			public ParentCache ResetParent(){
				PlacableObject.transform.SetParent(ParentTrans);
				PlacableObject.NoneOffsettedRotation = PlacableObject.transform.localRotation; // reset none offsetted
				PlacableObject.RotationOffset = Quaternion.identity; // reste rotation offset
				return this;
			}
			public ParentCache ReparentTo(Transform NewParent){
				PlacableObject.transform.SetParent(NewParent); // reparent
				return this;
			}
		}
		#endregion
	}
}
