using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections;

using BrunetonsAtmosphere;

namespace BrunetonsOcean
{
	/// <summary>
	/// 
	/// Generates a wave spectrum using the formula in the follow research paper.
	/// Can the found with a bit of googling.
	/// 
	/// WAVES SPECTRUM
	/// using "A unified directional spectrum for long and short wind-driven waves"
	/// T. Elfouhaily, B. Chapron, K. Katsaros, D. Vandemark
	/// Journal of Geophysical Research vol 102, p781-796, 1997
	/// 
	/// </summary>
	public class WaveSpectrumGPU : MonoBehaviour
	{
		
		//CONST DONT CHANGE
		const float WAVE_CM = 0.23f;	// Eq 59
		const float WAVE_KM = 370.0f;	// Eq 59
		
		/// <summary>
		/// This is the fourier transform size, must pow2 number. 
		/// </summary>
		public int m_size = 128;
		float m_fsize;
		
		/// <summary>
		/// A higher wind speed gives greater swell to the waves.
		/// </summary>
		public float m_windSpeed = 8.0f;

        /// <summary>
        /// Scales the height of the waves.
        /// </summary>
        public float m_waveAmp = 1.0f;

        /// <summary>
        /// A lower number means the waves last longer and 
        /// will build up larger waves.
        /// </summary>
        public float m_omega = 0.84f;

        /// <summary>
        /// The waves are made up of 4 layers of heights
        /// at different wave lengths. These grid sizes 
        /// are basically the wave length for each layer.
        /// </summary>
        public Vector4 m_gridSizes = new Vector4(5488, 392, 28, 2);
        Vector4 m_inverseGridSizes;

        /// <summary>
        /// strength of sideways displacement for each grid
        /// </summary>
        public Vector4 m_choppyness = new Vector4(2.3f, 2.1f, 1.3f, 0.9f);

        /// <summary>
        /// The material used to render the ocean mesh.
        /// </summary>
        public Material m_oceanMaterial;

        /// <summary>
        /// Compute the variance for the BRDF.
        /// </summary>
        public ComputeShader m_varianceShader;

        /// <summary>
        /// Material to initilize the spectrum before fourier transform.
        /// </summary>
        public Material m_initSpectrumMaterial, m_initDisplacementMat;

        /// <summary>
        /// Performs the fourier transform on the GPU.
        /// </summary>
        public Material m_fourierMaterial;

        /// <summary>
        /// The sky dome object.
        /// </summary>
        public GameObject m_skyGO;
        private Sky m_sky;

        /// <summary>
        /// Size of the variance texture.
        /// </summary>
        int m_varianceSize = 16;
		
		/// <summary>
		/// The fourier buffers and spectrum data.
		/// </summary>
		Texture2D m_spectrum01, m_spectrum23;
		RenderTexture[] m_fourierBuffer0, m_fourierBuffer1, m_fourierBuffer2, m_fourierBuffer3, m_fourierBuffer4;
		Texture2D m_WTable;
		
		/// <summary>
		/// The maps holding the height, slopes and displacements.
		/// </summary>
		RenderTexture m_map0, m_map1, m_map2, m_map3, m_map4;
		
		/// <summary>
		/// The variance for the BRDF.
		/// </summary>
		RenderTexture m_variance;
		
		/// <summary>
		/// Fourier object to do the fourier transform.
		/// </summary>
		FourierGPU m_fourier;

        /// <summary>
        /// The current buffer index.
        /// Flips between 0 and 1 each frame. 
        /// </summary>
        int m_idx = 0;

        void Start()
		{

            if(m_skyGO != null)
                m_sky = m_skyGO.GetComponent<Sky>();
			
			//Fourier size can not be greater than 256 because the
			//FourierGPU object uses a 8 bit texture for the 
			//butterfly look up table. This limitation could be
			//removed in theory if a look up table with more bits is used.
			if (m_size > 256)
			{
				Debug.Log("Fourier grid size must not be greater than 256, changing to 256");
                m_size = 256;
			}
			
			if (!Mathf.IsPowerOfTwo(m_size))
			{
				Debug.Log("Fourier grid size must be pow2 number, changing to nearest pow2 number");
                m_size = Mathf.NextPowerOfTwo(m_size);
			}
			
			m_fsize = (float)m_size;
            Vector4 offset = new Vector4(1.0f + 0.5f / m_fsize, 1.0f + 0.5f / m_fsize, 0, 0);

			float factor = 2.0f * Mathf.PI * m_fsize;
			m_inverseGridSizes = new Vector4(factor / m_gridSizes.x, factor / m_gridSizes.y, factor / m_gridSizes.z, factor / m_gridSizes.w);
			
			m_fourier = new FourierGPU(m_size, m_fourierMaterial);
			
			CreateRenderTextures();
			GenerateWavesSpectrum();
			CreateWTable();

			m_initSpectrumMaterial.SetTexture("_Spectrum01", m_spectrum01);
			m_initSpectrumMaterial.SetTexture("_Spectrum23", m_spectrum23);
			m_initSpectrumMaterial.SetTexture("_WTable", m_WTable);
			m_initSpectrumMaterial.SetVector("_Offset", offset);
			m_initSpectrumMaterial.SetVector("_InverseGridSizes", m_inverseGridSizes);

            m_initDisplacementMat.SetVector("_InverseGridSizes", m_inverseGridSizes);

        }

        /// <summary>
        /// Simulates the waves for time period.
        /// </summary>
        void Update()
        {

            InitWaveSpectrum(Time.time);

            //Perform fourier transform. If your having issues with the waves disappearing when the
            //screen is minimized try using tempory render textures for the fourier buffers instead.
            m_idx = m_fourier.PeformFFT(m_fourierBuffer0, m_fourierBuffer1, m_fourierBuffer2);
            m_fourier.PeformFFT(m_fourierBuffer3, m_fourierBuffer4);

            Graphics.Blit(m_fourierBuffer0[m_idx], m_map0);
            Graphics.Blit(m_fourierBuffer1[m_idx], m_map1);
            Graphics.Blit(m_fourierBuffer2[m_idx], m_map2);
            Graphics.Blit(m_fourierBuffer3[m_idx], m_map3);
            Graphics.Blit(m_fourierBuffer4[m_idx], m_map4);

            m_oceanMaterial.SetVector("_GridSizes", m_gridSizes);
            m_oceanMaterial.SetVector("_Choppyness", m_choppyness);
            m_oceanMaterial.SetTexture("_Variance", m_variance);
            m_oceanMaterial.SetTexture("_Map0", m_map0);
            m_oceanMaterial.SetTexture("_Map1", m_map1);
            m_oceanMaterial.SetTexture("_Map2", m_map2);
            m_oceanMaterial.SetTexture("_Map3", m_map3);
            m_oceanMaterial.SetTexture("_Map4", m_map4);

            //Need to apply the sky settings for ocean.
            if (m_sky != null)
                m_sky.UpdateMat(m_oceanMaterial);

        }

        void OnDestroy()
		{
            Destroy(m_map0);
            Destroy(m_map1);
            Destroy(m_map2);
            Destroy(m_map3);
            Destroy(m_map4);
            Destroy(m_variance);
            DestroyBuffer(m_fourierBuffer0);
            DestroyBuffer(m_fourierBuffer1);
            DestroyBuffer(m_fourierBuffer2);
            DestroyBuffer(m_fourierBuffer3);
            DestroyBuffer(m_fourierBuffer4);

        }

        private void DestroyBuffer(RenderTexture[] buffer)
        {
            Destroy(buffer[0]);
            Destroy(buffer[1]);
        }

		/// <summary>
		/// Create all the textures need to hold the data.
		/// </summary>
		void CreateRenderTextures()
		{
			RenderTextureFormat format = RenderTextureFormat.ARGBFloat;
			
			//These texture hold the actual data use in the ocean renderer
			m_map0 = new RenderTexture(m_size, m_size, 0, format, RenderTextureReadWrite.Linear);
			m_map0.filterMode = FilterMode.Trilinear;
			m_map0.wrapMode = TextureWrapMode.Repeat;
			m_map0.useMipMap = true;
			m_map0.Create();

			m_map1 = new RenderTexture(m_size, m_size, 0, format, RenderTextureReadWrite.Linear);
			m_map1.filterMode = FilterMode.Trilinear;
			m_map1.wrapMode = TextureWrapMode.Repeat;
			m_map1.useMipMap = true;
			m_map1.Create();
			
			m_map2 = new RenderTexture(m_size, m_size, 0, format, RenderTextureReadWrite.Linear);
			m_map2.filterMode = FilterMode.Trilinear;
			m_map2.wrapMode = TextureWrapMode.Repeat;
			m_map2.useMipMap = true;
			m_map2.Create();

            m_map3 = new RenderTexture(m_size, m_size, 0, format, RenderTextureReadWrite.Linear);
            m_map3.filterMode = FilterMode.Trilinear;
            m_map3.wrapMode = TextureWrapMode.Repeat;
            m_map3.useMipMap = true;
            m_map3.Create();

            m_map4 = new RenderTexture(m_size, m_size, 0, format, RenderTextureReadWrite.Linear);
            m_map4.filterMode = FilterMode.Trilinear;
            m_map4.wrapMode = TextureWrapMode.Repeat;
            m_map4.useMipMap = true;
            m_map4.Create();

            //These textures are used to perform the fourier transform
            m_fourierBuffer0 = new RenderTexture[2];
			m_fourierBuffer1 = new RenderTexture[2];
			m_fourierBuffer2 = new RenderTexture[2];
            m_fourierBuffer3 = new RenderTexture[2];
            m_fourierBuffer4 = new RenderTexture[2];

            CreateBuffer(m_fourierBuffer0, format);
			CreateBuffer(m_fourierBuffer1, format);
			CreateBuffer(m_fourierBuffer2, format);
            CreateBuffer(m_fourierBuffer3, format);
            CreateBuffer(m_fourierBuffer4, format);

            //These textures hold the specturm the fourier transform is performed on
            m_spectrum01 = new Texture2D(m_size, m_size, TextureFormat.RGBAFloat, false, true);
			m_spectrum01.filterMode = FilterMode.Point;
			m_spectrum01.wrapMode = TextureWrapMode.Repeat;
			
			m_spectrum23 = new Texture2D(m_size, m_size, TextureFormat.RGBAFloat, false, true);
			m_spectrum23.filterMode = FilterMode.Point;
			m_spectrum23.wrapMode = TextureWrapMode.Repeat;	
			
			m_WTable = new Texture2D(m_size, m_size, TextureFormat.RGBAFloat, false, true);
			m_WTable.filterMode = FilterMode.Point;
			m_WTable.wrapMode = TextureWrapMode.Clamp;	
			
			m_variance = new RenderTexture(m_varianceSize, m_varianceSize, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			m_variance.volumeDepth = m_varianceSize;
			m_variance.wrapMode = TextureWrapMode.Clamp;
			m_variance.filterMode = FilterMode.Bilinear;
            m_variance.dimension = TextureDimension.Tex3D;
			m_variance.enableRandomWrite = true;
			m_variance.useMipMap = true;
			m_variance.Create();

		}
		
		void CreateBuffer(RenderTexture[] tex, RenderTextureFormat format)
		{
			for(int i = 0; i < 2; i++)
			{
				tex[i] = new RenderTexture(m_size, m_size, 0, format, RenderTextureReadWrite.Linear);
				tex[i].filterMode = FilterMode.Point;
				tex[i].wrapMode = TextureWrapMode.Clamp;
				tex[i].Create();
			}
		}
		
		float sqr(float x) { return x*x; }

		float omega(float k) { return Mathf.Sqrt(9.81f * k * (1.0f + sqr(k / WAVE_KM))); } // Eq 24

		/// <summary>
		/// Recreates a statistcally representative model of a wave spectrum in the frequency domain.
		/// </summary>
		float Spectrum(float kx, float ky, bool omnispectrum = false)
		{

			float U10 = m_windSpeed;

			// phase speed
			float k = Mathf.Sqrt(kx * kx + ky * ky);
			float c = omega(k) / k;
			
			// spectral peak
			float kp = 9.81f * sqr(m_omega / U10); // after Eq 3
			float cp = omega(kp) / kp;
		
			// friction velocity
			float z0 = 3.7e-5f * sqr(U10) / 9.81f * Mathf.Pow(U10 / cp, 0.9f); // Eq 66
			float u_star = 0.41f * U10 / Mathf.Log(10.0f / z0); // Eq 60
		
			float Lpm = Mathf.Exp(- 5.0f / 4.0f * sqr(kp / k)); // after Eq 3
			float gamma = (m_omega < 1.0f) ? 1.7f : 1.7f + 6.0f * Mathf.Log(m_omega); // after Eq 3 // log10 or log?
			float sigma = 0.08f * (1.0f + 4.0f / Mathf.Pow(m_omega, 3.0f)); // after Eq 3
			float Gamma = Mathf.Exp(-1.0f / (2.0f * sqr(sigma)) * sqr(Mathf.Sqrt(k / kp) - 1.0f));
			float Jp = Mathf.Pow(gamma, Gamma); // Eq 3
			float Fp = Lpm * Jp * Mathf.Exp(-m_omega / Mathf.Sqrt(10.0f) * (Mathf.Sqrt(k / kp) - 1.0f)); // Eq 32
			float alphap = 0.006f * Mathf.Sqrt(m_omega); // Eq 34
			float Bl = 0.5f * alphap * cp / c * Fp; // Eq 31
		
			float alpham = 0.01f * (u_star < WAVE_CM ? 1.0f + Mathf.Log(u_star / WAVE_CM) : 1.0f + 3.0f * Mathf.Log(u_star / WAVE_CM)); // Eq 44
			float Fm = Mathf.Exp(-0.25f * sqr(k / WAVE_KM - 1.0f)); // Eq 41
			float Bh = 0.5f * alpham * WAVE_CM / c * Fm * Lpm; // Eq 40 (fixed)
			
			if(omnispectrum) return m_waveAmp * (Bl + Bh) / (k * sqr(k)); // Eq 30
		
			float a0 = Mathf.Log(2.0f) / 4.0f; 
			float ap = 4.0f; 
			float am = 0.13f * u_star / WAVE_CM; // Eq 59
			float Delta = (float)System.Math.Tanh(a0 + ap * Mathf.Pow(c / cp, 2.5f) + am * Mathf.Pow(WAVE_CM / c, 2.5f)); // Eq 57
		
			float phi = Mathf.Atan2(ky, kx);
		
			if (kx < 0.0f) return 0.0f;
		
			Bl *= 2.0f;
			Bh *= 2.0f;
			
			// remove waves perpendicular to wind dir
			float tweak = Mathf.Sqrt(Mathf.Max(kx/Mathf.Sqrt(kx*kx+ky*ky),0.0f));
		
			return m_waveAmp * (Bl + Bh) * (1.0f + Delta * Mathf.Cos(2.0f * phi)) / (2.0f * Mathf.PI * sqr(sqr(k))) * tweak; // Eq 677
		}
		
		Vector2 GetSpectrumSample(float i, float j, float lengthScale, float kMin)
		{
			float dk = 2.0f * Mathf.PI / lengthScale;
			float kx = i * dk;
			float ky = j * dk;
			Vector2 result = new Vector2(0.0f,0.0f);
			
			float rnd = UnityEngine.Random.value;
			
			if(Mathf.Abs(kx) >= kMin || Mathf.Abs(ky) >= kMin)
			{
				float S = Spectrum(kx, ky);
				float h = Mathf.Sqrt(S / 2.0f) * dk;
							
				float phi = rnd * 2.0f * Mathf.PI;
				result.x = h * Mathf.Cos(phi);
				result.y = h * Mathf.Sin(phi);
			}
			
			return result;
		}
		
		float GetSlopeVariance(float kx, float ky, Vector2 spectrumSample)
		{
			float kSquare = kx * kx + ky * ky;
			float real = spectrumSample.x;
			float img = spectrumSample.y;
			float hSquare = real * real + img * img;
			return kSquare * hSquare * 2.0f;
		}

		/// <summary>
		/// Generates the wave spectrum based on the 
		/// settings wind speed, wave amp and wave age.
		/// If these values change this function must be called again.
		/// </summary>
		void GenerateWavesSpectrum()
		{
			// Slope variance due to all waves, by integrating over the full spectrum.
			float theoreticSlopeVariance = 0.0f;
			float k = 5e-3f;
			while (k < 1e3f) 
			{
				float nextK = k * 1.001f;
				theoreticSlopeVariance += k * k * Spectrum(k, 0, true) * (nextK - k);
				k = nextK;
			}

			Color[] spectrum01 = new Color[m_size*m_size];
            Color[] spectrum23 = new Color[m_size*m_size];

			int idx;
			float i, j;
			float totalSlopeVariance = 0.0f;
			Vector2 sample12XY, sample12ZW;
			Vector2 sample34XY, sample34ZW;

            UnityEngine.Random.InitState(0);
			
			for (int x = 0; x < m_size; x++) 
			{
				for (int y = 0; y < m_size; y++) 
				{
					idx = x+y*m_size;
					i = (x >= m_size / 2) ? (float)(x - m_size) : (float)x;
					j = (y >= m_size / 2) ? (float)(y - m_size) : (float)y;
		
					sample12XY = GetSpectrumSample(i, j, m_gridSizes.x, Mathf.PI / m_gridSizes.x);
					sample12ZW = GetSpectrumSample(i, j, m_gridSizes.y, Mathf.PI * m_fsize / m_gridSizes.x);
					sample34XY = GetSpectrumSample(i, j, m_gridSizes.z, Mathf.PI * m_fsize / m_gridSizes.y);
					sample34ZW = GetSpectrumSample(i, j, m_gridSizes.w, Mathf.PI * m_fsize / m_gridSizes.z);

					spectrum01[idx].r = sample12XY.x;
					spectrum01[idx].g = sample12XY.y;
					spectrum01[idx].b = sample12ZW.x;
					spectrum01[idx].a = sample12ZW.y;
					
					spectrum23[idx].r = sample34XY.x;
					spectrum23[idx].g = sample34XY.y;
					spectrum23[idx].b = sample34ZW.x;
					spectrum23[idx].a = sample34ZW.y;
					
					i *= 2.0f * Mathf.PI;
					j *= 2.0f * Mathf.PI;
					
					totalSlopeVariance += GetSlopeVariance(i / m_gridSizes.x, j / m_gridSizes.x, sample12XY);
					totalSlopeVariance += GetSlopeVariance(i / m_gridSizes.y, j / m_gridSizes.y, sample12ZW);
					totalSlopeVariance += GetSlopeVariance(i / m_gridSizes.z, j / m_gridSizes.z, sample34XY);
					totalSlopeVariance += GetSlopeVariance(i / m_gridSizes.w, j / m_gridSizes.w, sample34ZW);
				}
			}

            m_spectrum01.SetPixels(spectrum01);
            m_spectrum01.Apply();

            m_spectrum23.SetPixels(spectrum23);
            m_spectrum23.Apply();

            m_varianceShader.SetFloat("_SlopeVarianceDelta", 0.5f * (theoreticSlopeVariance - totalSlopeVariance));
			m_varianceShader.SetFloat("_VarianceSize", (float)m_varianceSize);
			m_varianceShader.SetFloat("_Size", m_fsize);
			m_varianceShader.SetVector("_GridSizes", m_gridSizes);
			m_varianceShader.SetTexture(0, "_Spectrum01", m_spectrum01);
			m_varianceShader.SetTexture(0, "_Spectrum23", m_spectrum23);
			m_varianceShader.SetTexture(0, "des", m_variance);
			
			m_varianceShader.Dispatch(0,m_varianceSize/4,m_varianceSize/4,m_varianceSize/4);

		}

		/// <summary>
		/// Some of the values needed in the InitWaveSpectrum function can be precomputed.
		/// If the grid sizes change this function must called again.
		/// </summary>
		void CreateWTable()
		{
			//Some values need for the InitWaveSpectrum function can be precomputed
			Vector2 uv, st;
			float k1, k2, k3, k4, w1, w2, w3, w4;
			
			Color[] table = new Color[m_size*m_size];

			for (int x = 0; x < m_size; x++) 
			{
				for (int y = 0; y < m_size; y++) 
				{
					uv = new Vector2(x,y) / m_fsize;

		    		st.x = uv.x > 0.5f ? uv.x - 1.0f : uv.x;
		    		st.y = uv.y > 0.5f ? uv.y - 1.0f : uv.y;
		
			    	k1 = (st * m_inverseGridSizes.x).magnitude;
			    	k2 = (st * m_inverseGridSizes.y).magnitude;
			    	k3 = (st * m_inverseGridSizes.z).magnitude;
			    	k4 = (st * m_inverseGridSizes.w).magnitude;
					
					w1 = Mathf.Sqrt(9.81f * k1 * (1.0f + k1 * k1 / (WAVE_KM*WAVE_KM)));
					w2 = Mathf.Sqrt(9.81f * k2 * (1.0f + k2 * k2 / (WAVE_KM*WAVE_KM)));
					w3 = Mathf.Sqrt(9.81f * k3 * (1.0f + k3 * k3 / (WAVE_KM*WAVE_KM)));
					w4 = Mathf.Sqrt(9.81f * k4 * (1.0f + k4 * k4 / (WAVE_KM*WAVE_KM)));
					
					table[x+y*m_size].r = w1;
					table[x+y*m_size].g = w2;
					table[x+y*m_size].b = w3;
					table[x+y*m_size].a = w4;
				
				}
			}

            m_WTable.SetPixels(table);
            m_WTable.Apply();
					
		}

		/// <summary>
		/// Initilize the spectrum for a time period.
		/// </summary>
		void InitWaveSpectrum(float t)
		{
			RenderTexture[] buffers = new RenderTexture[]{ m_fourierBuffer0[1], m_fourierBuffer1[1], m_fourierBuffer2[1] };

			m_initSpectrumMaterial.SetFloat("_T", t);
			
			RTUtility.MultiTargetBlit(buffers, m_initSpectrumMaterial);

            RenderTexture[] buffers34 = new RenderTexture[] { m_fourierBuffer3[1], m_fourierBuffer4[1] };
            m_initDisplacementMat.SetTexture("_Buffer1", m_fourierBuffer1[1]);
            m_initDisplacementMat.SetTexture("_Buffer2", m_fourierBuffer2[1]);
            RTUtility.MultiTargetBlit(buffers34, m_initDisplacementMat);
        }
		
	}
}








