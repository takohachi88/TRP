#if HAS_VFX_GRAPH

using System;
using System.Collections.Generic;
using System.Linq;
using Trp;
using UnityEditor.VFX;
using UnityEngine;

namespace TrpEditor.VfxGraph
{
	/// <summary>
	/// カスタムシェーダーを利用するOutputコンテクストの親クラス。
	/// VFXShaderGraphParticleOutputを継承しているのはVFXParticleMeshOutputやVFXParticlePlanarPrimitiveOutputに準じているからであって、
	/// ShaderGraphを用いるわけではない。
	/// </summary>
	internal class VfxCustomShaderOutput : VFXShaderGraphParticleOutput
	{
		public enum ShaderPropertyType
		{
			Float,
			Float2,
			Float3,
			Float4,
			Int,
			Color,
			Bool,
			Texture2d,
		}

		[Serializable]
		public class ShaderProperty
		{
			[VFXSetting, SerializeField] public ShaderPropertyType Type;
			[VFXSetting, SerializeField] public string Name;
		}

		[VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), SerializeField, Multiline]
		protected string _shaderHeader;

		//配列はグラフ内で表示できない。
		[VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), SerializeField]
		protected ShaderProperty[] _shaderProperties;

		[VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), SerializeField]
		protected ShaderPass _shaderPass1 = new()
		{
			Lightode = TrpConstants.PassNames.SRP_DEFAULT_UNLIT,
		};
		[VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), SerializeField]
		protected ShaderPass _shaderPass2;

		internal static bool DoesReplacement(ShaderPass shaderPass)
			=> !string.IsNullOrEmpty(shaderPass?.Lightode) && (shaderPass.HlslFile || !string.IsNullOrEmpty(shaderPass.HlslText));

		[Serializable]
		public class ShaderPass
		{
			[VFXSetting, SerializeField] public string Lightode;
			[VFXSetting, SerializeField] public TextAsset HlslFile;
			[VFXSetting, SerializeField, Multiline] public string HlslText;
		}

		internal static IEnumerable<KeyValuePair<string, VFXShaderWriter>> PassReplacements(ShaderPass shaderPass, int index)
		{
			VFXShaderWriter fragmentWriter = new();
			if (shaderPass.HlslFile) fragmentWriter.Write(shaderPass.HlslFile);
			else fragmentWriter.Write(shaderPass.HlslText);
			yield return new KeyValuePair<string, VFXShaderWriter>("${VFXCustomPassFragment" + index + "}", fragmentWriter);

			VFXShaderWriter lightModeWriter = new();
			lightModeWriter.Write(shaderPass.Lightode);
			yield return new KeyValuePair<string, VFXShaderWriter>("${VFXCustomPassLightMode" + index + "}", lightModeWriter);
		}

		public override IEnumerable<string> additionalDefines
		{
			get
			{
				foreach (var kvp in base.additionalDefines) yield return kvp;

				if (DoesReplacement(_shaderPass1)) yield return "TRP_CUSTOM_PASS_1";
				if (DoesReplacement(_shaderPass2)) yield return "TRP_CUSTOM_PASS_2";
			}
		}

		public override IEnumerable<KeyValuePair<string, VFXShaderWriter>> additionalReplacements
		{
			get
			{
				foreach (var kvp in base.additionalReplacements) yield return kvp;

				if (DoesReplacement(_shaderPass1)) foreach (var kbp in PassReplacements(_shaderPass1, 1)) yield return kbp;
				if (DoesReplacement(_shaderPass2)) foreach (var kbp in PassReplacements(_shaderPass2, 2)) yield return kbp;
			}
		}

		protected override IEnumerable<VFXPropertyWithValue> inputProperties
		{
			get
			{
				foreach (var property in base.inputProperties) yield return property;

				if (_shaderProperties != null)
				{
					foreach (var property in _shaderProperties)
					{
						if (string.IsNullOrEmpty(property.Name)) continue;
						Type type = property.Type switch
						{
							ShaderPropertyType.Float => typeof(float),
							ShaderPropertyType.Float2 => typeof(Vector2),
							ShaderPropertyType.Float3 => typeof(Vector3),
							ShaderPropertyType.Float4 => typeof(Vector4),
							ShaderPropertyType.Int => typeof(int),
							ShaderPropertyType.Color => typeof(Color),
							ShaderPropertyType.Bool => typeof(bool),
							ShaderPropertyType.Texture2d => typeof(Texture2D),
							_ => throw new Exception(),
						};
						yield return new VFXPropertyWithValue(new VFXProperty(type, property.Name));
					}
				}
			}
		}

		public override VFXExpressionMapper GetExpressionMapper(VFXDeviceTarget target)
		{
			var mapper = base.GetExpressionMapper(target);

			switch (target)
			{
				case VFXDeviceTarget.CPU:
					{
						if (_shaderProperties != null)
						{
							foreach (var property in _shaderProperties)
							{
								if (string.IsNullOrEmpty(property.Name)) continue;
								mapper.AddExpression(inputSlots.First(s => s.name == property.Name).GetExpression(), property.Name, -1);
							}
						}
						break;
					}
				default:
					break;
			}

			return mapper;
		}

		protected override IEnumerable<VFXNamedExpression> CollectGPUExpressions(IEnumerable<VFXNamedExpression> slotExpressions)
		{
			foreach (var exp in base.CollectGPUExpressions(slotExpressions)) yield return exp;

			foreach (ShaderProperty property in _shaderProperties)
			{
				yield return slotExpressions.First(o => o.name == property.Name);
			}
		}

		public override sealed bool CanBeCompiled()
		{
			return (VFXLibrary.currentSRPBinder is VfxTrpBinder) && base.CanBeCompiled();
		}
	}
}

#endif