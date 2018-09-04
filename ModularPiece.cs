using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.ComponentModel;
using UnityEngine.UI;
using System.Linq;

namespace Modular{
	[System.Serializable]
	public class ModularPiece : ModularPlacableObject{
		#region public variables
		// public variables
		[Header("Modular piece boundary settings")]
		public Vector3 BoundSize = Vector3.zero;
		public Vector3 BoundsOffset = Vector3.zero;
		[Tooltip("Defines what the up axis of this object is")]

		[Header("Modular piece global settings")]
		public DirectionTypes UpDirection = DirectionTypes.Up;
		[Tooltip("Determins the snap methode to use as default for this object. Edge places object on the edges of the grid, center on the center of grid points and free form doesnt snap to the grid")]
		public SnapTypes GridSnapType = SnapTypes.Default;
		public GridCutTypes GridCutType = GridCutTypes.Default;
		public GridCutTypes GrassCutType = GridCutTypes.Default;

		[HideInInspector]
		public SnapAxis GridMaxSize = SnapAxis.X;
		[HideInInspector]		
		public SnapAxis GridMinSize = SnapAxis.X;
		[HideInInspector]
		public SnapAxis RealMaxSize = SnapAxis.X;

		[HideInInspector]
		public float CustomMaxSize;
		[HideInInspector]
		public float CustomMinSize;
		[HideInInspector]
		public float CustomRealMaxSize;

		[HideInInspector]
		public bool AlignableItem = true;

		// Appearance
		public bool UseTextureArray = false;
		[HideInInspector]public List<ModularTextureArray.ModularTextureArraySetting> TextureArrays = new List<ModularTextureArray.ModularTextureArraySetting> ();
		[HideInInspector]public List<int> TextureArraysDefaultIndex = new List<int> ();
		#endregion

		#region Private variables
		private string[] _TextureKeys = new string[0];
		private ModularSet _ParentSet;
		private string _ID;
		#endregion

		#region Public functions
		public void InitializeID(string ID){
			this._ID = ID;
		}
		public void InitializeColors(ColorLink[] Cols){
			RefillColorLinks ();
			Color[] LinkColors = new Color[Cols.Length];
			for (int i = 0; i < Cols.Length; i++) {
				LinkColors [i] = Cols [i].Color;
			}
			InitializeColors (LinkColors);
		}
		public void InitializeColors(Color[] Cols){
			RefillColorLinks ();
			if (Cols.Length == Colors.Length) {
				for (int i = 0; i < Colors.Length; i++) {
					Colors [i].Color = Cols[i];
				}
			}
		}
		public void InitializeColors(){
			RefillColorLinks ();
		}
		public void InitializePlaced(LayerMask Layer){
			this.gameObject.layer = Layer.ToLayer (); // set layer
			OnInitialize();
		}
		public void InitializeEditable(){
			this.gameObject.layer = ((AlignableItem)?Management.GameManager.I.Modular.ModularPieceTempLayer:Management.GameManager.I.Modular.ModularPieceIgnoreLayer).ToLayer (); // set layer
			this.RegisterUndoable(); // register piece as new undoable
			OnInitialize();
		}
		public void InitializeTextureData(string[] TextureKeys){
			if (TextureKeys == null) {
				InitializeTextureData ();
				return;}
			_TextureKeys = TextureKeys;
			RefillTextureLinks(); // refill texture links
		}
		public void InitializeTextureData(){
			string[] TempKeys = new string[TextureArrays.Count];
			for (int i = 0; i < TempKeys.Length; i++) {
				if (TextureArrays [i] != null && TextureArrays[i].TextureArray != null && TextureArrays[i].TextureArray.Textures.Count > 0) {
					if (TextureArrays [i].DefaultIndex < TextureArrays [i].TextureArray.Textures.Count) {
						TempKeys [i] = TextureArrays [i].TextureArray.Textures [TextureArrays [i].DefaultIndex].Key;
					} else {
						TempKeys [i] = TextureArrays [i].TextureArray.Textures [0].Key;
					}
				} else {
					TempKeys [i] = "";
				}

			}
			InitializeTextureData (TempKeys);
		}
		public void RegisterUndoable(){
			Management.GameManager.I.Utils.RegisterInstantiate(this.gameObject,(object[] ToDestoy)=>{
				// on destroy remove self from parent
				Destroy();
			},(object[] Data) =>{
				// on changed
				RemoveFromGrid(); // remove from grid
				UndoUtil.UndoData undodata = (UndoUtil.UndoData)Data.Find<UndoUtil.UndoData>();
				PlaceAtPos(undodata.Position); // place last pos
				transform.rotation = undodata.Rotation;
				transform.localScale = undodata.LocalScale;
				OnUpdateGrid(); // update grid
			});
		}
		public void SetCustomGridScale(float GridScale){
			GridMaxSize = SnapAxis.Custom;
			CustomMaxSize = GridScale;
		}
		#endregion

		#region base functions
		protected override void OnAwake ()
		{
			base.OnAwake ();
			Bounds = new AxisBounds (Position, BoundSize, transform);
			this.gameObject.name = this.gameObject.name.Replace ("(Clone)",""); // remove clone part from name
		}
		public void Initialize(ModularSet Set){
			this._ParentSet = Set;
		}
		public override void OnDeplaced ()
		{
			base.OnDeplaced ();
			SetGrass (true);
		}
		public override void OnPlaced ()
		{
			base.OnPlaced ();
			SetGrass (false);
		}
		public override void Destroy ()
		{
			ParentSet.RemoveModularPiece (this);
			base.Destroy ();
		}
		public override void OnUpdateGrid ()
		{
			UpdateGrid (Bounds.WorldCenter,Bounds.WorldExtends);
		}
		#endregion	

		#region Private voids
		private void SetGrass(bool visible){
			if (CutGrass) {
				Vector3 Center = Bounds.WorldCenter;
				Vector3 Extends = Bounds.WorldExtends;
				Management.GameManager.I.Terrain.SetDetailLayerVisible (visible, Center, Extends * 2f);
			}
		}
		#endregion

		#region Get / Set
		public string[] TextureKeys{
			get{
				return _TextureKeys;
			}
			set{
				_TextureKeys = value;
			}
		}
		public string[] CurrentTextureKeys{
			get{
				string[] CurrentKeys = new string[TextureArrays.Count];
				for (int i = 0; i < Textures.Count; i++) {
					for (int x = 0; x < Textures [i].Tuples.Count; x++) {
						CurrentKeys [Textures [i].Tuples[x].SetID] = Textures [i].Texture.Key;
					}
				}

				for (int i = 0; i < CurrentKeys.Length; i++) {
					if (CurrentKeys [i] == null) {
						CurrentKeys [i] = "";
					}
				}
				return CurrentKeys;
			}

		}
		public string ID{
			get{
				return _ID;
			}
		}
		public ModularSet ParentSet{
			get{
				return _ParentSet;
			}
		}
		public override bool CutGrid{
			get{
				if (GridCutType == GridCutTypes.Ignore) {
					return false;
				} else if (GridCutType == GridCutTypes.Cut) {
					return true;
				} else {
					return DefaultCutGrid;
				}
			}
		}
		#endregion

		#region Self Virtuals
		public virtual void OnParentDestroy(){}
		public virtual void OnInitialize(){}
		public virtual bool DefaultCutGrid{
			get{
				return false;
			}
		}
		public virtual bool DefaultCutGrass{
			get{
				return false;
			}
		}
		public virtual bool CutGrass{
			get{
				if (GrassCutType == GridCutTypes.Ignore) {
					return false;
				} else if (GrassCutType == GridCutTypes.Cut) {
					return true;
				} else {
					return DefaultCutGrass;
				}
			}
		}
		#endregion

		#region Virtuals
		protected override void RefillTextureLinks ()
		{
			base.RefillTextureLinks ();
			if (UseTextureArray) {
				List<Utils.RenderSet.Set> RenderSets = RenderSet.Sets;
				List<TextureLink> Links = new List<TextureLink> ();
				for (int i = 0; i < RenderSets.Count; i++) {
					if (TextureKeys.Length <= i || !Management.GameManager.I.Modular.TextureDataDictionary.ContainsKey (TextureKeys [i])) {continue;}
					ModularTextureData Texture = Management.GameManager.I.Modular.TextureDataDictionary [TextureKeys [i]];
					TextureLink Link = new TextureLink (Texture, TextureArrays [i].TextureArray, this, RenderSet, i);
					Link.ApplyTextureData (RenderSets[i].Renderer,Texture);
					Links.Add(Link);
				}
				Textures = Links;
			}
		}
		public override Vector3 UpAxis {
			get {
				switch (UpDirection) {
				case DirectionTypes.Up:
					return Vector3.up;
					break;
				case DirectionTypes.Down:
					return Vector3.down;
					break;
				case DirectionTypes.Forward:
					return Vector3.forward;
					break;
				case DirectionTypes.Left:
					return Vector3.left;
					break;
				case DirectionTypes.Right:
					return Vector3.right;
					break;
				case DirectionTypes.Back:
					return new Vector3(0,0,-1);
					break;
				default:
					return Vector3.up;
					break;
				};
			}
		}
		public override Vector3 GridCenter {
			get {
				return (ParentSet)?ParentSet.WorldGridCenter:Vector3.zero;
			}
		}
		public override Vector3 AxisBoundsOffset {
			get {
				Vector3 vec = Vector3.zero;
				vec += Vector3.right * BoundsOffset.x;
				vec += Vector3.up * BoundsOffset.y;
				vec += Vector3.forward * BoundsOffset.z;
				vec = Vector3.Scale (vec,transform.lossyScale);
				return vec;
			}
		}
		public override Transform LocalSpace {
			get {
				return (ParentSet)?ParentSet.LocalSpace:null;
			}
		}
		public override SnapTypes SnapType {
			get {
				if (GridSnapType == SnapTypes.Default) {
					return DefaultSnapType;
				} else {
					return GridSnapType;
				}
			}
		}
		public override AxisBounds MaxExtendBounds {
			get {
				if (DefinesBoundarys) {
					return ParentSet.GetBoundaryIncludingPiece (this); // return max extends inculding this piece at all times
				} else {
					return ParentSet.MaxExtendBounds; // return parent max extends
				}
			}
		}
		protected override bool ColorsFromMaterial {
			get {
				return !this.UseTextureArray;
			}
		}
		/// <summary>
		/// Returns the local position relative to the grid center of the parent
		/// </summary>
		/// <value>The local position.</value>
		public override Vector3 LocalPosition {
			get {
				return base.LocalPosition;
			}
			set {
				transform.localPosition = value;
			}
		}
		public override AxisBounds Bounds {
			get {
				_Bounds.center = Position + AxisBoundsOffset;
				return base.Bounds;
			}
			set {
				base.Bounds = value;
			}
		}
		public override void UpdateTransform ()
		{
			ParentSet.UpdateModularSet (); // update modular set
		}
		public override bool CanDuplicate (out string Message)
		{
			if (Management.GameManager.I.Economy.CanSpend (Management.GameManager.I.Economy.EvaluateModularPiece (this))) {
				Message = "";
				return true;
			}

			Message = "You have insufficient funds";
			return false;
		}
		public override ModularPlacableObject Duplicate ()
		{
			ModularPiece DuplicatedPiece = (ModularPiece)base.Duplicate ();
			DuplicatedPiece.Initialize (ParentSet); // initialize modular set
			DuplicatedPiece.InitializeID(ID);
			DuplicatedPiece.InitializeTextureData (this.CurrentTextureKeys);
			DuplicatedPiece.InitializeColors (this.Colors);
			this.ParentSet.AddModularPiece(DuplicatedPiece); // add duplicated piece to modular sets
			DuplicatedPiece.RegisterUndoable();
			return DuplicatedPiece;
		}
		public override float Scale {
			get {
				switch(GridMaxSize){
				case SnapAxis.Custom:
					return CustomMaxSize;
					break;
				case SnapAxis.X:
					return BoundSize.x;
					break;
				case SnapAxis.Y:
					return BoundSize.y;
					break;
				case SnapAxis.Z:
					return BoundSize.z;
					break;
				default:
					return base.Scale; // return base as default
					break;
				}
			}
		}
		public override float RealScale {
			get {
				switch(RealMaxSize){
				case SnapAxis.Custom:
					return CustomRealMaxSize;
					break;
				case SnapAxis.X:
					return BoundSize.x;
					break;
				case SnapAxis.Y:
					return BoundSize.y;
					break;
				case SnapAxis.Z:
					return BoundSize.z;
					break;
				default:
					return base.RealScale; // return base as default
					break;
				}
			}
		}
		public override float MinScale {
			get {
				switch(GridMinSize){
				case SnapAxis.Custom:
					return CustomMinSize;
					break;
				case SnapAxis.X:
					return BoundSize.x;
					break;
				case SnapAxis.Y:
					return BoundSize.y;
					break;
				case SnapAxis.Z:
					return BoundSize.z;
					break;
				default:
					return base.MinScale; // return base as default
					break;
				}
			}
		}
		#endregion

		#region Gizmos / scene UI
		public void AutoCalculateValues(){
			if (RenderSet.MeshFilters.Length > 0) {
				Bounds Bound = RenderSet.CombinedMeshFilterBounds;
				BoundSize = Bound.size;
				BoundsOffset = Bound.center;

				// max scale
				Vector3 MaxAxis = BoundSize.SingleAxis (true,false);
				if (MaxAxis.x != 0) {
					GridMaxSize = SnapAxis.X;
				} else if (MaxAxis.y != 0) {
					GridMaxSize = SnapAxis.Y;
				} else {
					GridMaxSize = SnapAxis.Z;
				}

				// real max
				RealMaxSize = GridMaxSize; // set same as max

				// min
				Vector3 MinAxis = BoundSize.SingleAxis (false);
				if (MinAxis.x != 0) {
					GridMinSize = SnapAxis.X;
				} else if (MinAxis.y != 0) {
					GridMinSize = SnapAxis.Y;
				} else {
					GridMinSize = SnapAxis.Z;
				}
			}
		}
		private void OnDrawGizmosSelected(){
			Color Blue = new Color();
			ColorUtility.TryParseHtmlString ("#03A9F4", out Blue);

			Color Yellow = new Color();
			ColorUtility.TryParseHtmlString ("#FFB300", out Yellow);


			if (BoundSize == Vector3.zero && this.gameObject.GetComponent<Renderer> () != null) { // auto calculate
				BoundSize = this.gameObject.GetComponent<Renderer> ().bounds.size;
			}
			if (BoundsOffset == Vector3.zero && this.gameObject.GetComponent<MeshFilter> () != null) { // auto calculate
				BoundsOffset = this.gameObject.GetComponent<MeshFilter>().sharedMesh.bounds.center;
			}

			Vector3 Pos = (transform.position + AxisBoundsOffset).ToLocalRotationFull(transform.rotation,transform.position);
			Matrix4x4 cubeMatrix = Matrix4x4.TRS (Pos,transform.rotation,Vector3.Scale(BoundSize,transform.lossyScale));
			Gizmos.matrix = cubeMatrix;
			Gizmos.color = Color.white;
			Gizmos.DrawWireCube (Vector3.zero,Vector3.one);

			Gizmos.color = Yellow;
			Gizmos.DrawRay (Vector3.zero, UpAxis);

			// render grid

			Matrix4x4 cubeMatrix2 = Matrix4x4.TRS (transform.position,transform.rotation,transform.lossyScale);
			Gizmos.matrix = cubeMatrix2;

			Gizmos.DrawWireCube(Vector3.zero,new Vector3(MinScale,0,MinScale));


			Gizmos.color = Blue;
			Gizmos.DrawWireCube(Vector3.zero,new Vector3(Scale,0,Scale));
		}
		#endregion

		#region Enums
		public enum GridCutTypes{
			Default,
			Cut,
			Ignore
		}
		public enum DirectionTypes{
			Up,
			Down,
			Forward,
			Back,
			Right,
			Left,
		}
		#endregion
	}
}
