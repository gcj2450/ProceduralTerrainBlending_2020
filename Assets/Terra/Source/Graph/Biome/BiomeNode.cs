﻿using System;
using Terra.CoherentNoise;
using Terra.Graph.Generators;
using Terra.Source;
using Terra.Structures;
using Terra.Terrain;
using Terra.Util;
using UnityEngine;
using XNode;

namespace Terra.Graph.Biome {
	[CreateNodeMenu("Biomes/Biome")]
	public class BiomeNode: PreviewableNode {
		public struct BiomeMapResult {
			/// <summary>
			/// A structure containing the weights of the height, temperature, and moisture maps.
			/// The returned structure is a 3D array where the first two indices are the x & y 
			/// coordinates while the last index is the weight of the height, temperature, and 
			/// moisture maps (in that order).
			/// </summary>
			public float[,,] Values;

			/// <summary>
			/// The lowest value within <see cref="Values"/>
			/// </summary>
			public float Min;

			/// <summary>
			/// The highest value within <see cref="Values"/>
			/// </summary>
			public float Max;

			public BiomeMapResult(float[,,] values, float min, float max) {
				Values = values;
				Min = min;
				Max = max;
			}
		}

		[Output]
		public BiomeNode Output;

		/// <summary>
		/// Name of the biome
		/// </summary>
		public string Name;

		public Color PreviewColor;

		/// <summary>
		/// Splats that this Biome will display
		/// </summary>
		[Input(ShowBackingValue.Never)] 
		public SplatDetailNode SplatDetails;

        [Input(ShowBackingValue.Never)]
        public TreeDetailNode Trees;

        [Input(ShowBackingValue.Never)]
        public GrassDetailNode Grass;

        [Input(ShowBackingValue.Never)]
        public ObjectDetailNode Objects;

        [Input]
		public float Blend = 1f;

		public bool UseHeightmap;
		public Vector2 HeightmapMinMaxMask = new Vector2(0, 1);

		public bool UseTemperature;
		public Vector2 TemperatureMinMaxMask = new Vector2(0, 1);

		public bool UseMoisture;
		public Vector2 MoistureMinMaxMask = new Vector2(0, 1);

		private EndNode EndNode => TerraConfig.Instance.Graph.GetEndNode();
		private GeneratorSampler _heightmapSampler => EndNode.GetHeightmapSampler();
		private GeneratorSampler _temperatureSampler => EndNode.GetTemperatureSampler();
		private GeneratorSampler _moistureSampler => EndNode.GetMoistureMapSampler();

		private static object _asyncLock = new object();

		public override object GetValue(NodePort port) {
			return this;
		}

		public override Texture2D DidRequestTextureUpdate(int size, float spread) {
			return GetPreviewTexture(size, spread);
		}

		public SplatDetailNode[] GetSplatInputs() {
			return GetInputValues<SplatDetailNode>("SplatDetails", null);
		}
        
        public TreeDetailNode[] GetTreeInputs() {
            return GetInputValues<TreeDetailNode>("Trees");
        }

        public GrassDetailNode[] GetGrassInputs() {
            return GetInputValues<GrassDetailNode>("Grass");
        }

        public ObjectDetailNode[] GetObjectInputs() {
            return GetInputValues<ObjectDetailNode>("Objects");
        }
        
		/// <summary>
		/// Gets the heights for the height, temperature, and moisture maps. If 
		/// a map isn't used, 0 is set for its value. The returned float structure 
		/// is filled as follows:
		/// [heightmap val, temperature val, moisture val]
		/// </summary>
		/// <param name="x">X coordinate to sample</param>
		/// <param name="y">Y coordinate to sample</param>
		/// <param name="position">Position in Terra grid</param>
		/// <param name="resolution">Resolution of the map to sample for</param>
		/// <returns></returns>
		public float[] GetMapHeightsAt(int x, int y, GridPosition position, int resolution, float spread, int length) {
            if (!UseHeightmap && !UseTemperature && !UseMoisture) {
                return new[] { 1f, 1f, 1f };
            }

            float[] heights = new float[3];

            if (UseHeightmap && _heightmapSampler != null) {
                       heights[0] = _heightmapSampler.GetValue(x, y, position, resolution, spread, length);
			}
            if (UseTemperature && _temperatureSampler != null) {
                heights[1] = _temperatureSampler.GetValue(x, y, position, resolution, spread, length);
			}
            if (UseMoisture && _moistureSampler != null) {
                heights[2] = _moistureSampler.GetValue(x, y, position, resolution, spread, length);
			}

            return heights;
		}

		/// <summary>
		/// Calculate the weight of the biome based on the height, 
		/// temperature, and moisture
		/// </summary>
		public float[,] GetWeights(GridPosition position, int resolution, float spread, int length) {
			float[,] weights = new float[resolution, resolution];

			Constraint hc = new Constraint(HeightmapMinMaxMask.x, HeightmapMinMaxMask.y);
			Constraint tc = new Constraint(TemperatureMinMaxMask.x, TemperatureMinMaxMask.y);
			Constraint mc = new Constraint(MoistureMinMaxMask.x, MoistureMinMaxMask.y);
			float blend = 0.1f;

			Func<bool, int> boolToInt = (i) => i ? 1 : 0;
			int mapsInUseCount = boolToInt(UseHeightmap) + boolToInt(UseTemperature) + boolToInt(UseMoisture);

			MathUtil.LoopXY(resolution, (x, y) => {
				Func<GeneratorSampler, float> sample = (sampler) => Mathf.Clamp01(sampler.GetValue(x, y, position, resolution, spread, length));
				float weight = 0;

				if (UseHeightmap) {
					weight += hc.Weight(sample(_heightmapSampler), blend);
				}
				if (UseTemperature) {
					weight += tc.Weight(sample(_temperatureSampler), blend);
				}
				if (UseMoisture) {
					weight += mc.Weight(sample(_moistureSampler), blend);
				}

				weights[x, y] = weight / mapsInUseCount;
			});

			return weights;
        }
		
		/// <summary>
		/// Creates a map of weights where each weight representing the probability 
		/// between 0 and 1 that the biome will appear
		/// </summary>
		/// <param name="values"></param>
		/// <param name="min"></param>
		/// <param name="max"></param>
		//public float[,] GetWeightedValues() {
		//	//Constraint
		//	Constraint hc = new Constraint(HeightmapMinMaxMask.x, HeightmapMinMaxMask.y);
		//	Constraint tc = new Constraint(TemperatureMinMaxMask.x, TemperatureMinMaxMask.y);
		//	Constraint mc = new Constraint(MoistureMinMaxMask.x, MoistureMinMaxMask.y);

		//	int resolution = values.GetLength(0);
		//	float[,] map = new float[resolution, resolution];

		//	//Normalize values
		//	for (int x = 0; x < resolution; x++) {
		//		for (int y = 0; y < resolution; y++) {
		//			if (!UseHeightmap && !UseTemperature && !UseMoisture) {
		//				map[x, y] = 1f;
		//				continue;
		//			}

		//			float hv = MathUtil.Map01(values[x, y, 0], min, max);
		//			float tv = MathUtil.Map01(values[x, y, 1], min, max);
		//			float mv = MathUtil.Map01(values[x, y, 2], min, max);

		//			float val = 0;
		//			int count = 0;
			
		//			//Gather heights that fit set min/max
		//			if (UseHeightmap && hc.Fits(hv)) {
		//				val += hc.Weight(hv, Blend);
		//				count++;
		//			}
		//			if (UseTemperature && tc.Fits(tv)) {
		//				val += tc.Weight(tv, Blend);
		//				count++;
		//			}
		//			if (UseMoisture && mc.Fits(mv)) {
		//				val += mc.Weight(mv, Blend);
		//				count++;
		//			}

		//			val = count > 0 ? val / count : 0;
		//			map[x, y] = Mathf.Clamp01(val);
		//		}
		//	}

		//	return map;
		//}

		/// <summary>
		/// Create a map of weights each representing the weights of 
		/// the height, temperature, and moisture maps.
		/// </summary>
		/// <param name="position">Position in Terra grid units to poll the generators</param>
		/// <param name="resolution">Resolution of the map to create</param>
		/// <param name="spread">Optional spread value to poll the generator with</param>
		/// <param name="length">Optional length parameter that represents the length of a tile</param>
		/// <returns>
		/// A structure containing the weights of the height, temperature, and moisture maps.
		/// The returned structure is a 3D array where the first two indices are the x & y 
		/// coordinates while the last index is the weight of the height, temperature, and 
		/// moisture maps (in that order).
		/// </returns>
		//public BiomeMapResult GetMapsValues(GridPosition position, int resolution, float spread = -1f, int length = -1) {
  //          lock (_asyncLock) {
  //              float[,,] heights = new float[resolution, resolution, 3];

  //              //Update generators
  //              SetHeightmapSampler();
  //              SetTemperatureSampler();
  //              SetMoistureSampler();

  //              //Default spread/length to TerraConfig settings
  //              spread = spread == -1f ? TerraConfig.Instance.Generator.Spread : spread;
  //              length = length == -1 ? TerraConfig.Instance.Generator.Length : length;

  //              //Track min/max
  //              MinMaxRecorder minMax = new MinMaxRecorder();

  //              //Fill heights structure and set min/max values
  //              for (int x = 0; x < resolution; x++) {
  //                  for (int y = 0; y < resolution; y++) {
  //                      float[] generated = GetMapHeightsAt(x, y, position, resolution, spread, length);

  //                      for (int z = 0; z < 3; z++) {
  //                          float height = generated[z];
  //                          heights[x, y, z] = height;
  //                          minMax.Register(height);
  //                      }
  //                  }
  //              }
                
  //              MinMaxResult result = minMax.GetMinMax();
  //              return new BiomeMapResult(heights, result.Min, result.Max);
  //          }
		//}

        /// <summary>
        /// Calculates the min and max result for this Biome's 
        /// connected Generator
        /// </summary>
        /// <param name="resolution">resolution of remap calculation</param>
        public MinMaxResult CalculateMinMax(int resolution) {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;

            for (int x = 0; x < resolution; x++) {
                for (int y = 0; y < resolution; y++) {
                    float[] heights = GetMapHeightsAt(x, y, GridPosition.Zero, resolution, resolution, 1);
                    MinMaxResult localMinMax = MathUtil.GetMinMax(heights);

                    if (localMinMax.Min < min) {
                        min = localMinMax.Min;
                    }
                    if (localMinMax.Max > max) {
                        max = localMinMax.Max;
                    }
                }
            }

            return new MinMaxResult(min, max);
        }

		/// <summary>
		/// Creates a texture previewing this biome with the passed size used 
		/// for the width and height
		/// </summary>
		/// <param name="size">width & height</param>
		/// <returns></returns>
		private Texture2D GetPreviewTexture(int size, float spread) {
			Texture2D tex = new Texture2D(size, size);
			float[,] weights = GetWeights(GridPosition.Zero, size, spread, 1);

			//Set texture
			for (int x = 0; x < size; x++) {
				for (int y = 0; y < size; y++) {
					float val = weights[x, y];
					tex.SetPixel(x, y, new Color(val, val, val, 1f));
				}
			}

			tex.Apply();
			return tex;
		}


		//private Texture2D GetPreviewTexture(int size) {
		//          Texture2D tex = new Texture2D(size, size);

		//	GetWeights
		//          BiomeMapResult mapVals = GetMapsValues(GridPosition.Zero, size, size, 1);
		//          float[,] normalized = GetWeightedValues(mapVals.Values, mapVals.Min, mapVals.Max);

		//          //Set texture
		//          for (int x = 0; x < size; x++) {
		//              for (int y = 0; y < size; y++) {
		//                  float val = normalized[x, y];
		//                  tex.SetPixel(x, y, new Color(val, val, val, 1f));
		//              }
		//          }

		//          tex.Apply();
		//          return tex;
		//      }
	}
}