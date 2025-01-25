using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
	/// <summary>
	/// 被写界深度。
	/// </summary>
	[Serializable, VolumeComponentMenu("TRP/DepthOfField"), SupportedOnRenderPipeline(typeof(TrpAsset))]
	public class DepthOfField : VolumeComponent, IPostProcessComponent
	{
		/// <summary>
		/// 被写界深度の実装モード。
		/// </summary>
		public enum Mode
		{
			None,

			/// <summary>
			/// URPのBokehモードの移植。
			/// </summary>
			BokehUrp,
		}

		public static readonly float FocusDistanceMin = 0.1f;
		public static readonly float ApertureMin = 1f;
		public static readonly float ApertureMax = 32f;
		public static readonly float FocalLengthMin = 1f;
		public static readonly float FocalLengthMax = 300f;

		public EnumParameter<Mode> mode = new(Mode.None);


		/// <summary>
		/// The distance to the point of focus.
		/// </summary>
		[Tooltip("The distance to the point of focus.")]
		public MinFloatParameter focusDistance = new MinFloatParameter(10f, FocusDistanceMin);

		/// <summary>
		/// The ratio of aperture (known as f-stop or f-number). The smaller the value is, the shallower the depth of field is.
		/// </summary>
		[Tooltip("The ratio of aperture (known as f-stop or f-number). The smaller the value is, the shallower the depth of field is.")]
		public ClampedFloatParameter aperture = new ClampedFloatParameter(5.6f, ApertureMin, ApertureMax);

		/// <summary>
		/// The distance between the lens and the film. The larger the value is, the shallower the depth of field is.
		/// </summary>
		[Tooltip("The distance between the lens and the film. The larger the value is, the shallower the depth of field is.")]
		public ClampedFloatParameter focalLength = new ClampedFloatParameter(50f, FocalLengthMin, FocalLengthMax);

		/// <summary>
		/// The number of aperture blades.
		/// </summary>
		[Tooltip("The number of aperture blades.")]
		public ClampedIntParameter bladeCount = new ClampedIntParameter(5, 3, 9);

		/// <summary>
		/// The curvature of aperture blades. The smaller the value is, the more visible aperture blades are. A value of 1 will make the bokeh perfectly circular.
		/// </summary>
		[Tooltip("The curvature of aperture blades. The smaller the value is, the more visible aperture blades are. A value of 1 will make the bokeh perfectly circular.")]
		public ClampedFloatParameter bladeCurvature = new ClampedFloatParameter(1f, 0f, 1f);

		/// <summary>
		/// The rotation of aperture blades in degrees.
		/// </summary>
		[Tooltip("The rotation of aperture blades in degrees.")]
		public ClampedFloatParameter bladeRotation = new ClampedFloatParameter(0f, -180f, 180f);

		public bool IsActive() => mode.value != Mode.None || SystemInfo.graphicsShaderLevel < 35;
	}
}