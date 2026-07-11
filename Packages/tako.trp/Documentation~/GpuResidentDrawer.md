# GPU Resident Drawer

TRPのGPU Resident Drawer（GRD）は、通常の`MeshRenderer`をBatchRendererGroupの間接描画へ移し、CPU側の描画送信負荷を減らします。TRP Assetの`GPU Resident Drawer Mode`を`InstancedDrawing`に設定してください。

`GPU Occlusion Culling`を有効にすると、TRP既存の`DepthNormalsOnly`パスを2回実行し、Unity提供のGRD深度ピラミッドを更新します。TRP独自の`DepthOnly`パスは追加しません。深度に寄与する不透明シェーダーは`DepthNormalsOnly`パスを実装してください。

## シェーダー側の要件

- `#pragma target 4.5`以上を指定し、`#pragma multi_compile _ DOTS_INSTANCING_ON`を追加します。PPLL OITのようにUAVを使うものはSM 5.0のままにします。
- `Common.hlsl`をincludeする前に、ライトプローブやRender Boundsを独自に扱わないシェーダーでは次を定義します。

  ```hlsl
  #define UNITY_SETUP_DOTS_SH_COEFFS
  #define UNITY_SETUP_DOTS_RENDER_BOUNDS
  ```

- 頂点入力に`UNITY_VERTEX_INPUT_INSTANCE_ID`を加え、頂点関数の先頭で`UNITY_SETUP_INSTANCE_ID(input)`を呼びます。フラグメントでインスタンス単位の値を読む場合は、VaryingsにもIDを転送して同じセットアップを行います。
- オブジェクト行列は`TransformObjectToWorld`、`TransformObjectToHClip`、`GetVertexInputs`など、TRP/Commonの変換関数から取得します。独自に固定のObjectToWorld行列を参照しません。
- `MaterialPropertyBlock`はGRDの対象外になるため、個別値を使わない設計にします。共有値は`UnityPerMaterial`、全体共有のテクスチャは`Shader.SetGlobalTexture`などのグローバル値を使います。
- GPU Occlusion Cullingを使う不透明シェーダーには、同じ頂点変形・アルファクリップを反映した`DepthNormalsOnly`パスが必要です。頂点カラーの揺れもこのパスで再現し、可視面と深度面を一致させます。

## 対応済みシェーダー

- `TRP/Unlit`、`TRP/Toon`：通常描画・ShadowCaster/Outline/DepthNormalsOnlyを含めてDOTS Instancingへ対応。
- `TRP/WbOit`、`TRP/PpllOit`：透明OITの描画送信をGRDへ対応。透明物は深度ピラミッドの遮蔽物には含めません。

## 効果が見込める条件

多数の同種または少数のマテリアルに分かれるMeshRendererを描画するケースで、GRDは主にCPU時間を削減します。ワールド座標のグローバルテクスチャ参照や頂点カラーの頂点変形はGRDと両立します。

遮蔽カリングは、画面内に大量にあるが前景の不透明物に隠れるinstanceが多い場合に有効です。見通しの良い平面マップチップ配置では、深度プリパスとピラミッド生成のGPUコストが効果を上回ることがあります。
