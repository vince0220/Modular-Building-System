using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using Management;
using DreamInCode.BuildingSystem;
using System.Diagnostics;
using System;
using System.ComponentModel;

namespace Modular.States{
	public class PlacePlacableObject : IControllableState {

		#region Base variables
		private ModularBuildingSystem System;
		private ModularPlacableObject ScopedObject;
		private Delegates.GenericDataCallbak OnPlacedCallback;
		private System.Action<object[]> OnCanceledCallback;
		private System.Func<ModularPlacableObject,bool> CanPlaceCallback;
		#endregion

		#region Private variables
		// general
		private LocalGridSystem LocalVisualGrid;
		private SnapTypes SnapType;
		private StackTypes StackType;
		private int AlignToSurface = 0;
		private FastPlaceDetailSettings DetailSettings;
		private float RandomOffset;
		private bool LocalSpace = true;

		// Screen
		private Vector3 ObjectScreenObject;

		// Rotation
		private bool IsRotating = false;
		private IEnumerator RotationEnum;
		private float RotationDelay = 0.2f;
		private float RotationSpeed = 20f;
		private SceneHandle RotationRing;
		private Quaternion StartRotation;
		private Quaternion SnapRotation;

		// Fast placement
		private Vector3 FastPlaceStartVector; // position when starting fast placement
		private Vector3 FastPlacementOffset; // vector used to offset position in normal placement
		private Vector3 FastPlaceAxis;
		private Vector3 FastPlaceTempPosition;
		private bool FastPlacingSetOffset = false;
		private bool IsFastPlacing = false; // determines wheter fast placing is going on
		private SceneHandle FastPlaceHandles;
		private LineIndicator Indicator;

		// UI
		private UI.Toolbar.Toolbar SettingsBar;
		#endregion

		#region IState voids
		public void Enter (List<object> GenData = null)
		{
			//GameManager.I.ChangeGameState (new GameStates.ModularGameState()); // change to modular state
			System = GenData.Find<ModularBuildingSystem> (true); // create link to Modular building system
			ScopedObject = GenData.Find<ModularPlacableObject>(true); // link to scoped piece
			OnPlacedCallback = GenData.Find<Delegates.GenericDataCallbak>(true); // set on place callback
			OnCanceledCallback = GenData.Find<System.Action<object[]>>(true); // set on cancle callback
			CanPlaceCallback = GenData.Find<System.Func<ModularPlacableObject,bool>>(true);

			// init extra data
			LocalVisualGrid = (LocalGridSystem)GenData.Find<LocalGridSystem>(); // set cached local visual grid

			if ((object)GenData.FindObject<PlaceObjectCache> () != null) {
				Cache = GenData.Find<PlaceObjectCache> (); // set cache
			} else {
				InitDefaultValues();
			}

			Indicator = (Indicator != null)?Indicator:GameManager.I.Utils.NewLineIndicator (Vector3.zero, Vector3.zero); // set indicator if is null

			Init (); // initialize

			// init detail
			ScopedObject.SetCollidersActive (false,true);
			DetailSettings.OnValuesChanged = OnValueChanged;
			UpdateTransform ();
		}

		public void Update ()
		{
			CheckUserInput (); // check user input
		}

		public void ForceExit ()
		{
			OnExit ();
		}

		public void Exit ()
		{
			RemoveBindings (); // remove key bindings and layer disables
			DestorySceneHandles (); // destroy scene handles on final leave
			Management.GameManager.I.UI.DestroyToolbar(SettingsBar); // destory toolbar
		}
		public void ReEnterExit(){
			RemoveBindings (); // remove key bindings and layer disables
		}
		public void Interrupt(){
			OnExit ();
		}
		#endregion

		#region Initialize voids
		private void Init(){
			InitKeyBindings ();
			//InitVisualGrid (); // init the visual 3d grid
			InitSceneUI (); // init the 3d scene UI
			InitUI();
			InitBaseTransform ();
		}
		private void InitUI(){
			List<UI.Toolbar.HeaderAction> Settings = new List<UI.Toolbar.HeaderAction> ();
			List<UI.Toolbar.HeaderAction> ColorActions = new List<UI.Toolbar.HeaderAction> ();

			// add actions
			#region Movement actions

			if (ScopedObject.PositionRestriction == PositionRestrictions.None) { // only add settings if doesnt have restrictions
				#region Snap Type Settings
				int SnapTypeIndex = 0;
				if (SnapType == SnapTypes.Cross){SnapTypeIndex = 0;}
				else if(SnapType == SnapTypes.Edge){SnapTypeIndex = 1;}
				else if(SnapType == SnapTypes.Center){ SnapTypeIndex = 2;} 
				else if(SnapType == SnapTypes.FreeForm){ SnapTypeIndex = 3;}


				Settings.Add (new UI.Toolbar.HeaderAction (
					UI.Toolbar.HeaderActionType.Toggle,
					new IconPack.IconType[] {
						IconPack.IconType.GridCross,
						IconPack.IconType.GridEdge,
						IconPack.IconType.GridCenter,
						IconPack.IconType.NoGrid
					}, new string[] {
					"Grid cross",
					"Grid edge",
					"Grid center",
					"No grid"
				}, new System.Action<object[]>[] {
					(objects) => {
						SnapType = SnapTypes.Cross;
					},
					(objects) => {
						SnapType = SnapTypes.Edge;
					},
					(objects) => {
						SnapType = SnapTypes.Center;
					},
					(objects) => {
						SnapType = SnapTypes.FreeForm;
					}
				}, SnapTypeIndex, UI.ToolTipComponent.AlignSide.Top
				));

				#endregion
				#region Align To Surface
				Settings.Add (new UI.Toolbar.HeaderAction (
					UI.Toolbar.HeaderActionType.Toggle,
					new IconPack.IconType[] {
						IconPack.IconType.AlignStraight,
						IconPack.IconType.AlignSurface,
						IconPack.IconType.AlignSet,
						IconPack.IconType.NoAlign
					}, new string[] {
						"Align to surface position",
						"Align to surface",
						"Align to anything",
						"Don't align"
					}, new System.Action<object[]>[] {
						(objects)=>{
							AlignToSurface = 0;
						},
						(objects) => {
							AlignToSurface = 1;
						},
						(objects) => {
							AlignToSurface = 2;
						},
						(objects)=>{
							AlignToSurface = 3;
						}
					},AlignToSurface, UI.ToolTipComponent.AlignSide.Top
				));
				#endregion
				#region Stack Types
				int StackIndex = 0;
				if(StackType == StackTypes.Center){StackIndex = 1;}
				else if(StackType == StackTypes.Disabled){StackIndex = 2;}
				Settings.Add (new UI.Toolbar.HeaderAction (
					UI.Toolbar.HeaderActionType.Toggle,
					new IconPack.IconType[] {
						IconPack.IconType.BoundaryStacking,
						IconPack.IconType.CenterStacking,
						IconPack.IconType.NoStacking
					}, new string[] {
						"Boundary stacking",
						"Center stacking",
						"No stacking"
					}, new System.Action<object[]>[] {
						(objects) => {
							StackType = StackTypes.Boundary;
						},
						(objects) => {
							StackType = StackTypes.Center;
						},
						(objects) => {
							StackType = StackTypes.Disabled;
						}
					}, StackIndex, UI.ToolTipComponent.AlignSide.Top
				));
				#endregion
				#region Local/world Space
				int LocalSetting = (LocalSpace)?0:1;
				Settings.Add (new UI.Toolbar.HeaderAction (
					UI.Toolbar.HeaderActionType.Boolean,
					new IconPack.IconType[] {
						IconPack.IconType.LocalSpace,
						IconPack.IconType.WorldSpace
					}, new string[] {
						"Local space",
						"World space"
					}, new System.Action<object[]>[] {
						(objects) => {
							LocalSpace = true;
						},
						(objects) => {
							LocalSpace = false;
						}
					}, LocalSetting, UI.ToolTipComponent.AlignSide.Top
				));
				#endregion
			}

			#region Colors
			ModularPlacableObject.ColorGroup[] ColorGroups = new Modular.ModularPlacableObject[]{ ScopedObject }.ColorGroups ();
			if (ColorGroups.Length < 8) { // limit colors to 8
				for (int i = 0; i < ColorGroups.Length; i++) {
					ColorActions.Add (InitColorSwatch (ColorGroups, i));
				}
			}
			#endregion
			
			
			#endregion

			// create toolbar settings
			UI.Toolbar.ToolbarSettings TempSettings = new UI.Toolbar.ToolbarSettings {
				HeaderSections = new UI.Toolbar.HeaderSection[] { // movement actions section
					new UI.Toolbar.HeaderSection (Settings.ToArray ()),
					new UI.Toolbar.HeaderSection (ColorActions.ToArray ())
				},
				ToolbarIconSize = 28f,
				Title = "Advanced",
				ScreenPadding = 52,
				MaxLength = 40,
				SettingsToolTip = "Extra",
				TransparentOnHover = true,
				Content = (ScopedObject.PositionRestriction == PositionRestrictions.None)?new object[]{
					DetailSettings
				}:new object[0]
			};

			// init toolbar
			if (SettingsBar == null) {
				SettingsBar = GameManager.I.UI.InitToolbar (TempSettings,false);
			} else {
				for (int i = 0; i < SettingsBar.ToolbarSettings.HeaderSections.Length; i++) {
					if (TempSettings.HeaderSections.Length > i) {
						for (int x = 0; x < SettingsBar.ToolbarSettings.HeaderSections [i].Actions.Length; x++) {
							if (TempSettings.HeaderSections [i].Actions.Length > x) {
								SettingsBar.ToolbarSettings.HeaderSections [i].Actions [x].ClickCallbacks = TempSettings.HeaderSections [i].Actions [x].ClickCallbacks;
							}
						}
					}
				}
			}
		}
		private UI.Toolbar.HeaderAction InitColorSwatch(ModularPlacableObject.ColorGroup[] ColorGroups, int ColorIndex){
			return new UI.Toolbar.HeaderAction(ColorGroups[ColorIndex].Color,(object[] Data)=>{
				ColorGroups[ColorIndex].Color = Data.Find<Color>();
			},UI.ToolTipComponent.AlignSide.Top);
		}
		private void InitSceneUI(){
			if (RotationRing == null) {
				RotationRing = GameObject.Instantiate (GameManager.I.Modular.RotationRingPrefab, Vector3.zero, Quaternion.identity).GetComponent<SceneHandle> ();
			} else {
				RotationRing.UpdateLocalScale (); // update scale
			}
			if (FastPlaceHandles == null) {
				FastPlaceHandles = GameObject.Instantiate (GameManager.I.Modular.FastMoveHandlePrefab, Vector3.zero, ScopedObject.Rotation).GetComponent<SceneHandle> ();
			} else {
				FastPlaceHandles.UpdateLocalScale (); // update scale
			}

			RotationRing.Interactable = false;
			FastPlaceHandles.Interactable = false;

			HideRotationRing (); // hide ring after init
			HideFastPlaceHandles(); // hide fast place handles
		}
		private void InitDefaultValues(){
			SnapType = ScopedObject.SnapType;
			StackType = (ScopedObject.DefinesBoundarys)?StackTypes.Center:StackTypes.Disabled;
			AlignToSurface = (ScopedObject.DefinesBoundarys) ? 3 : 1;
			DetailSettings = new FastPlaceDetailSettings ();
			DetailSettings._Scale = ScopedObject.transform.localScale;
		}
		private void InitKeyBindings(){
			// disable game manager functions
			GameManager.I.Utils.DisableLayer(UtilityController.ProcessLayer.Selection);
			GameManager.I.Utils.RegisterLayerTracker (UtilityController.ProcessLayer.HUD,OnHUDEnable,OnHUDDisable);

			GameManager.I.Select.DeselectAll (); // deselect all previous

			// init key bindings
			GameManager.I.Keys.AddOnDownCallback(GameManager.I.Constants.C_MODULAR_ROTATE,()=>{
				// on rotate down
				StartRotate();
			});
			GameManager.I.Keys.AddOnUpCallback (GameManager.I.Constants.C_MODULAR_ROTATE,()=>{
				// on rotate up
				EndRotate();
			});
			// fast place key bindings
			GameManager.I.Keys.AddOnHoldCallback (GameManager.I.Constants.C_LEFT_SHIFT, () => {
				// on left shift hold
				OnFastPlaceHold(new Vector3(0,1,0),true);
			});
			GameManager.I.Keys.AddOnUpCallback (GameManager.I.Constants.C_LEFT_SHIFT, () => {
				// on left shift up
				OnFastPlaceUp();
			});
			// fast place key bindings
			GameManager.I.Keys.AddOnHoldCallback (GameManager.I.Constants.C_LEFT_CTRL_AND_LEFT_SHIFT, () => {
				// on left shift hold
				OnFastPlaceHold(new Vector3(1,0,1));
			});
			GameManager.I.Keys.AddOnUpCallback (GameManager.I.Constants.C_LEFT_CTRL_AND_LEFT_SHIFT, () => {
				// on left shift up
				OnFastPlaceUp();
			});	
		}
		#endregion

		#region Functinality

		#region Global voids
		private void InitBaseTransform(){
			// random scale
			if (DetailSettings.RandomScale) {
				int MinMax = UnityEngine.Random.Range (0, 2);
				MinMax = (MinMax <= 0) ? -1 : 1;
				float AverageScale = (DetailSettings.ScaleX + DetailSettings.ScaleY + DetailSettings.ScaleZ) / 3;
				RandomOffset = Mathf.Clamp (((UnityEngine.Random.Range (0, DetailSettings.RandomPercentage)) / 100) * AverageScale,0.1f, float.MaxValue) * MinMax;
			} else {
				RandomOffset = 0f;
			}

			// random rotation
			Vector3 Eulers = new Vector3(0,0,0);

			if (DetailSettings.RandomRotationX) {Eulers.x += UnityEngine.Random.Range (0, 360);}
			if (DetailSettings.RandomRotationY) {Eulers.y += UnityEngine.Random.Range (0, 360);}
			if (DetailSettings.RandomRotationZ) {Eulers.z += UnityEngine.Random.Range (0, 360);}

			ScopedObject.RotationOffset = Quaternion.Euler (ScopedObject.RotationOffset.eulerAngles + Eulers);
		}
		private void OnValueChanged(){
			UpdateTransform ();
		}
		private void RemoveBindings(){
			GameManager.I.Keys.ClearAllCallbacks (GameManager.I.Constants.C_MODULAR_ROTATE); // remove rotating callbacks
			GameManager.I.Keys.ClearAllCallbacks (GameManager.I.Constants.C_LEFT_CTRL_AND_LEFT_SHIFT); // remove Left ctrl callbacks
			GameManager.I.Keys.ClearAllCallbacks (GameManager.I.Constants.C_LEFT_SHIFT); // remove Left shift callbacks
			GameManager.I.Utils.RemoveLayerTracker(UtilityController.ProcessLayer.HUD,OnHUDEnable,OnHUDDisable);
			GameManager.I.Utils.EnableLayer(UtilityController.ProcessLayer.Selection); // enable selection layer
			DetailSettings.OnValuesChanged -= OnValueChanged;
		}
		private void DestorySceneHandles(){
			FastPlaceHandles.Destoy(); // destoy fast place handles
			RotationRing.Destoy (); // destory rotation ring
			//LocalVisualGrid.Destoy(); // destoy visual grid
			Indicator.Destroy(); // destoy indicator
		}
		private void SetObjectScreenOffset(Vector3 StartPosition){
			ObjectScreenObject = StartPosition - Camera.main.ScreenToWorldPoint (
				new Vector3(Input.mousePosition.x,
					Input.mousePosition.y,
					Camera.main.WorldToScreenPoint(StartPosition).z
				)
			);
		}
		private void OnHUDEnable(){

		}
		private void OnHUDDisable(){
			OnExit (); // exit
		}
		private void OnGameStatesEnable(){
			// do nothing
		}
		private void OnGameStatesDisable(){
			OnExit (); // exit
		}
		#endregion

		#region Placement
		private void UpdateTransform(){
			InitBaseTransform ();

			Vector3 NewScale = new Vector3 (
				Mathf.Clamp(DetailSettings.ScaleX + RandomOffset,0.1f,float.MaxValue),
				Mathf.Clamp(DetailSettings.ScaleY+ RandomOffset,0.1f,float.MaxValue),
				Mathf.Clamp(DetailSettings.ScaleZ + RandomOffset,0.1f,float.MaxValue)
			);
			ScopedObject.transform.localScale = NewScale;
		}
		private void PlacePlacable(){
			SelectionHitOBJ HitObj = SelectionSystem.getMouseFowardLocation (new GameObject[]{ ScopedObject.gameObject},false, GameManager.I.Constants.TerrainLayer);
			
			if (AlignToSurface == 0) {
				HitObj = SelectionSystem.getMouseFowardLocation (new GameObject[]{ ScopedObject.gameObject },false, GameManager.I.Modular.ModularPieceTempLayer, GameManager.I.Constants.TerrainLayer);
				HitObj.HitNormal = Vector3.up; // clamp normal default
			} else if (AlignToSurface == 1) {
				HitObj = SelectionSystem.getMouseFowardLocation (new GameObject[]{ ScopedObject.gameObject },false, GameManager.I.Modular.ModularPieceTempLayer, GameManager.I.Constants.TerrainLayer);
			} else if (AlignToSurface == 2) {
				HitObj = SelectionSystem.getMouseFowardLocation (new GameObject[]{ ScopedObject.gameObject },false, GameManager.I.Modular.ModularPieceTempLayer,GameManager.I.Modular.ModularPieceLayer, GameManager.I.Constants.TerrainLayer);
			}

			ScopedObject.PlaceAtPos (HitObj.getHitPos(), SnapType,(AlignToSurface == 1 || AlignToSurface == 2)?(Vector3?)HitObj.HitNormal:null	,(LocalSpace)?ScopedObject.LocalSpace:null, FastPlacementOffset);

			if (GameManager.I.Modular.ScopedSet != null) { // is not building in a set
				ScopedObject.AutoStack (StackType, GameManager.I.Modular.ScopedSet);
			} else {
				ScopedObject.AutoStack (StackType,()=>{
					return Management.GameManager.I.Modular.OverlappingModularSets(ScopedObject,(StackType == StackTypes.Boundary));
				});
			}

			//UpdateVisualGrid ();
		}
		private void RenderIndicator(){
			RaycastHit Hit;
			Vector3 EndPosition = ScopedObject.Position;
			EndPosition.y -= int.MaxValue;
			if(Physics.Raycast(ScopedObject.Position + (new Vector3(0,0.1f,0)),Vector3.down,out Hit,200,GameManager.I.Constants.TerrainLayer | GameManager.I.Modular.ModularPieceTempLayer)){
				EndPosition = Hit.point;
			}
			Indicator.UpdatePosition (ScopedObject.Position,EndPosition);
		}
		#endregion

		#region Rotation
		private void RotatePlacable(){
			RotateWithMouse(); // rotate placable object
		}
		private void StartRotate(){
			CancelRotationEnum (); // cancle last rotation
			RotationEnum = Rotation (); // set new rotation enum
			System.Mono.StartCoroutine (RotationEnum); // start new rotation enum
		}
		private void EndRotate(){
			if (!IsRotating) {
				ScopedObject.RotationOffset = ScopedObject.RotationOffset.Multiply(Quaternion.Euler (0,90,0));
			}
			CancelRotationEnum ();
			PlacePlacable ();
		}
		private void CancelRotationEnum(){
			IsRotating = false;
			if (RotationEnum != null) {
				System.Mono.StopCoroutine (RotationEnum);
				RotationEnum = null;
			}

			// Scene UI
			HideRotationRing ();
		}
		private void RotateWithMouse(){
			ScopedObject.PlaceAtPos (ScopedObject.Position,SnapTypes.FreeForm, ScopedObject.LocalSpace); // update all values
			if (!RotationRing.gameObject.activeSelf) {ShowRotationRing ();}; // ensure seen
			// set new rotation based on mouse X axis input
			float MouseMove = Input.GetAxis ("Mouse X");

			ScopedObject.transform.rotation = SnapRotation; // set to rotation offset
			ScopedObject.transform.Rotate (new Vector3(0,MouseMove * RotationSpeed,0),(ScopedObject.SpaceRestriction == SpaceRestrictions.None)?Space.Self: Space.Self); // rotate based on space restriction
			SnapRotation = ScopedObject.transform.rotation; // set snap rotation
			ScopedObject.RotationOffset = ScopedObject.RotationOffset.SnapRotation (15,SnapRotation); // set rotation offset

			// set scene UI
			UpdateRotateRing();
			//UpdateVisualGrid ();
		}
		private void HideRotationRing(){
			RotationRing.gameObject.SetActive (false);
		}
		private void ShowRotationRing(){
			RotationRing.gameObject.SetActive (true);
			UpdateRotateRing ();
		}
		private void OnStartMouseRotate(){
			StartRotation = ScopedObject.NoneOffsettedRotation;
			SnapRotation = ScopedObject.RotationOffset;
			IsRotating = true; // rotation delay has past, rotating towards mouse starts
			ShowRotationRing(); // scene UI update
			RotationRing.UpdateLocalScale ();
		}

		// visual grid
		private void InitVisualGrid(){
			if (LocalVisualGrid == null) { // only init if not already set
				LocalVisualGrid = GameObject.Instantiate (GameManager.I.Modular.LocalGridPrefab, Vector3.zero, Quaternion.identity).GetComponent<LocalGridSystem> ().Hide (); // initialize local grid and hide afterwards
				LocalVisualGrid.GridSpacing = ScopedObject.Scale;
			}
		}
		private void UpdateVisualGrid(){
			ScopedObject.MaxExtendBounds.RenderLocalBounds (Color.yellow);
			UnityEngine.Debug.DrawRay (ScopedObject.MaxExtendBounds.center, Vector3.up * 100f,Color.cyan);
			LocalVisualGrid.Show (); // ensure visibility
			LocalVisualGrid.CoverAxisBounds(ScopedObject.MaxExtendBounds,SnapType,ScopedObject.Bounds.WorldCenter,ScopedObject.MaxExtendBounds.LocalBottom.y + 0.05f,ScopedObject.LocalSpace);
		}
		private void UpdateRotateRing(){
			RotationRing.transform.position = ScopedObject.transform.position;
			RotationRing.transform.rotation = ScopedObject.transform.rotation;
		}	
		#endregion

		#region Fast Placement
		private Vector3 WorldMousePositionFastPlace{
			get{
				Vector3 CurScreenPos = new Vector3 (Input.mousePosition.x, Input.mousePosition.y, Camera.main.WorldToScreenPoint (FastPlaceTempPosition).z);
				return Camera.main.ScreenToWorldPoint (CurScreenPos) + ObjectScreenObject;
			}
		}
		private void OnFastPlaceHold(Vector3 IdentityAxis,bool SetOffset = false){
			if (!IsFastPlacing) {
				OnFastPlaceStart (IdentityAxis,SetOffset);
			}

			if(IdentityAxis == FastPlaceAxis){ // check whether its the same itteration
				OnFastPlaceUpdate ();// update when fast placing
			}
		}
		private void OnFastPlaceUp(){
			// exit fast placement
			IsFastPlacing = false; // disable fast place

			CalculateOffset ();// calculate offset vector

			HideFastPlaceHandles (); // hide handles
		}
		private void CalculateOffset(){
			if(FastPlacingSetOffset){
				FastPlacementOffset += ScopedObject.Position - FastPlaceStartVector; // calculate offset
				FastPlacementOffset.x = 0; // zero out X
				FastPlacementOffset.z = 0; // zero out Z
				FastPlacingSetOffset = false; // reset set offset
				ScopedObject.LastYStack = ScopedObject.Position.y; // set last stack position

				if (FastPlacementOffset != Vector3.zero) {
					Indicator.Show ();
				} else {
					Indicator.Hide ();
				}
			}
		}
		private void OnFastPlaceStart(Vector3 IdentityAxis,bool SetOffset = false){
			IsFastPlacing = true; // set is fast placing
			FastPlaceTempPosition = ScopedObject.Position;
			FastPlaceStartVector = FastPlaceTempPosition; // set start position
			SetObjectScreenOffset(FastPlaceStartVector); // set screen offset
			FastPlaceAxis = IdentityAxis;
			FastPlacingSetOffset = SetOffset;

			// set handles
			List<int> ShowHandles = new List<int>();
			if (IdentityAxis.x != 0) {
				ShowHandles.Add (2);
			}
			if (IdentityAxis.y != 0) {
				ShowHandles.Add (1);
			}
			if (IdentityAxis.z != 0) {
				ShowHandles.Add (0);
			}

			FastPlaceHandles.ShowOnly (ShowHandles.ToArray());
		}
		private void OnFastPlaceUpdate(bool SingleAxis = true){
			ShowFastPlaceHandles (); // ensure show handle
			UpdateFastPlaceHandles(); // update handle

			Vector3 Centerd = WorldMousePositionFastPlace - FastPlaceStartVector;
			Vector3 WorldOffset = Vector3.Scale(Centerd.ToWorldRotation (ScopedObject.LocalSpace.forward, Vector3.zero),FastPlaceAxis); // world offset

			if (SingleAxis) {
				WorldOffset = WorldOffset.SingleAxis (); // get highest single axis
			}

			Vector3 LocalOffset = WorldOffset.ToLocalRotation(ScopedObject.LocalSpace.forward,Vector3.zero);

			FastPlaceTempPosition = FastPlaceStartVector + LocalOffset; // set temp position
			ScopedObject.PlaceAtPos (FastPlaceTempPosition, SnapType, ScopedObject.LocalSpace); // place at position
		}
		private void HideFastPlaceHandles(){
			FastPlaceHandles.gameObject.SetActive (false);
		}
		private void ShowFastPlaceHandles(){
			FastPlaceHandles.gameObject.SetActive (true);
		}
		private void UpdateFastPlaceHandles(){
			FastPlaceHandles.transform.position = ScopedObject.Position;
			FastPlaceHandles.transform.rotation = ScopedObject.Rotation;
		}
		#endregion

		#endregion

		#region Checks
		private void CheckUserInput(){
			// check actions
			if (IsRotating) {
				RotatePlacable ();
			} else if(IsFastPlacing){
				// on fast place
			} else {
				PlacePlacable (); // no user input just place object
			}
			RenderIndicator(); // render indicator

			// check input
			if (Input.GetKeyDown(KeyCode.Escape)) {
				OnExit (); // right click exit
			} else if (Input.GetMouseButtonDown (0)) {
				if (GameManager.I.Utils.IsEnabled (UtilityController.ProcessLayer.GameStates)) {
					OnPlace (); // on place
				}
			}
		}
		#endregion

		#region Final functions
		private void OnPlace(){
			if (CanPlaceCallback.Invoke (ScopedObject)) {
				// on placed, invoke callback and send scoped object and parent transform as data
				ScopedObject.SetCollidersActive (true, true);
				OnPlacedCallback.Invoke (new object[] {
					// global values
					ScopedObject,
					LocalVisualGrid,
					Cache
				});
			}
		}
		private void OnExit(){
			OnCanceledCallback.Invoke (new object[]{ScopedObject}); // invoke on canceled callback
		}
		#endregion

		#region Get / setters
		public PlaceObjectCache Cache{
			get{
				if (IsFastPlacing && FastPlacingSetOffset) {
					CalculateOffset (); // calculate current offset
				}

				return new PlaceObjectCache{
					IsRotating = this.IsRotating,
					IsFastPlacing = this.IsFastPlacing,
					FastPlaceIdentity = this.FastPlaceAxis,
					FastPlaceOffset = this.FastPlacementOffset,
					FastPlaceStart = this.FastPlaceStartVector,
					FastPlacingSetOffset = this.FastPlacingSetOffset,
					StartRotation = this.StartRotation,
					SnapRotation = this.SnapRotation,
					RotationRing = this.RotationRing,
					FastPlaceHandles = this.FastPlaceHandles,
					LastYStack = ScopedObject.LastYStack,
					LineIndicator = this.Indicator,
					ToolbarCache = this.SettingsBar,
					ColorLinks = this.ScopedObject.Colors,
					StackType = this.StackType,
					AlignToSurface = this.AlignToSurface,
					SnapType = this.SnapType,
					FastSettings = this.DetailSettings,
					LocalSpace = this.LocalSpace
				};
			}
			set {
				// set cache
				if (value.IsRotating) {
					// set rotation values
					this.IsRotating = value.IsRotating;
					this.StartRotation = value.StartRotation;
					this.SnapRotation = value.SnapRotation;
				}

				if (value.IsFastPlacing) {
					// set fast placing values
					this.FastPlaceAxis = value.FastPlaceIdentity;
					this.FastPlaceStartVector = value.FastPlaceStart;
					this.FastPlacingSetOffset = value.FastPlacingSetOffset;
				}

				// set defaults
				this.FastPlacementOffset = value.FastPlaceOffset;
				this.FastPlaceHandles = value.FastPlaceHandles;
				this.RotationRing = value.RotationRing;
				this.Indicator = value.LineIndicator;
				this.SettingsBar = value.ToolbarCache;
				this.StackType = value.StackType;
				this.SnapType = value.SnapType;
				this.AlignToSurface = value.AlignToSurface;
				this.DetailSettings = value.FastSettings;
				this.LocalSpace = value.LocalSpace;

				// init colors
				Modular.ModularPlacableObject.ColorLink[] Links = this.ScopedObject.Colors;
				if (value.ColorLinks != null && Links.Length == value.ColorLinks.Length) {
					for (int i = 0; i < Links.Length; i++) {
						Links [i].Color = value.ColorLinks [i].Color;
					}
				}

				ScopedObject.LastYStack = value.LastYStack;
			}
		}
		#endregion

		#region Enums
		private IEnumerator Rotation(){
			yield return new WaitForSeconds (RotationDelay); // wait for rotation delay before rotate towards mouse
			OnStartMouseRotate();
		}
		#endregion

		#region Structs
		public struct PlaceObjectCache{
			// booleans
			public bool IsFastPlacing;
			public bool IsRotating;
			public bool FastPlacingSetOffset;

			// Vectors
			public Vector3 FastPlaceStart;
			public Vector3 FastPlaceOffset;
			public Vector3 FastPlaceIdentity;
			public Quaternion StartRotation;
			public Quaternion SnapRotation;

			// scene handles
			public SceneHandle FastPlaceHandles;
			public SceneHandle RotationRing;
			public LineIndicator LineIndicator;

			// UI
			public UI.Toolbar.Toolbar ToolbarCache;

			// floats
			public float LastYStack;

			// settings
			public bool LocalSpace;
			public SnapTypes SnapType;
			public StackTypes StackType;
			public int AlignToSurface;
			public Modular.ModularPlacableObject.ColorLink[] ColorLinks;
			public FastPlaceDetailSettings FastSettings;
		}
		#endregion

		#region Detail Settings
		public class FastPlaceDetailSettings:UI.UIDynamicClass, INotifyPropertyChanged{
			#region INotifyPropertyChanged implementation
			public event PropertyChangedEventHandler PropertyChanged;

			private void OnPropertyChanged(string Name){
				PropertyChangedEventHandler handler = PropertyChanged;
				if (handler != null) {
					handler (this, new PropertyChangedEventArgs(Name));
					if (OnValuesChanged != null) {
						OnValuesChanged.Invoke ();
					}
				}
			}
			#endregion
			#region UIDynamicClass implementation

			public string DynamicTitle {
				get {
					return "Advanced settings";
				}
			}

			#endregion

			public Action OnValuesChanged;

			#region Grid Settings
			private float _GridScale;

			//[UI.UIBasicSlider(0,"Grid Size",0,20)]
			public float GridScale{
				get{
					return _GridScale;
				}
				set{
					_GridScale = value;
					OnPropertyChanged ("GridScale");
				}
			}
			#endregion

			#region Scale Properties
			// scale
			public Vector3 _Scale;
			private bool _RandomScale = false;
			private float _RandomScalePercentage = 0f;

			[UI.UIBasicSlider(1,"Scale X",0.1f,10)]
			public float ScaleX{
				get{
					return _Scale.x;
				}
				set{
					_Scale.x = value;
					OnPropertyChanged ("Scale");
				}
			}
			[UI.UIBasicSlider(2,"Scale Y",0.1f,10)]
			public float ScaleY{
				get{
					return _Scale.y;
				}
				set{
					_Scale.y = value;
					OnPropertyChanged ("Scale");
				}
			}
			[UI.UIBasicSlider(3,"Scale Z",0.1f,10)]
			public float ScaleZ{
				get{
					return _Scale.z;
				}
				set{
					_Scale.z = value;
					OnPropertyChanged ("Scale");
				}
			}

			[UI.UIToggle(0,"Random scale")]
			public bool RandomScale{
				get{
					return _RandomScale;
				}
				set{
					_RandomScale = value;
					OnPropertyChanged ("RandomScale");
				}
			}

			[UI.UIBasicSlider(4,"Random percentage",0,100,true)]
			public float RandomPercentage{
				get{
					return _RandomScalePercentage;
				}
				set{
					_RandomScalePercentage = value;
					OnPropertyChanged ("RandomScalePercentage");
				}
			}
			#endregion

			#region Rotation Properties
			private bool _RandomRotationX;
			private bool _RandomRotationY;
			private bool _RandomRotationZ;

			[UI.UIToggle(5,"Random X rotation")]
			public bool RandomRotationX{
				get{
					return _RandomRotationX;
				}
				set{
					_RandomRotationX = value;
					OnPropertyChanged ("RandomRotationX");
				}
			}
			[UI.UIToggle(6,"Random Y rotation")]
			public bool RandomRotationY{
				get{
					return _RandomRotationY;
				}
				set{
					_RandomRotationY = value;
					OnPropertyChanged ("RandomRotationY");
				}
			}
			[UI.UIToggle(7,"Random Z rotation")]
			public bool RandomRotationZ{
				get{
					return _RandomRotationZ;
				}
				set{
					_RandomRotationZ = value;
					OnPropertyChanged ("RandomRotationZ");
				}
			}
			#endregion
		}
		#endregion
	}
}
