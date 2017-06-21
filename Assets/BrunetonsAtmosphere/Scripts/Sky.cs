using UnityEngine;
using System.IO;

namespace BrunetonsAtmosphere
{

    public class Sky : MonoBehaviour
    {
        const float SCALE = 1000.0f;

        const int TRANSMITTANCE_WIDTH = 256;
        const int TRANSMITTANCE_HEIGHT = 64;
        const int TRANSMITTANCE_CHANNELS = 3;

        const int IRRADIANCE_WIDTH = 64;
        const int IRRADIANCE_HEIGHT = 16;
        const int IRRADIANCE_CHANNELS = 3;

        const int INSCATTER_WIDTH = 256;
        const int INSCATTER_HEIGHT = 128;
        const int INSCATTER_DEPTH = 32;
        const int INSCATTER_CHANNELS = 4;

        public bool m_showSkyMap = false;

        public string m_filePath = "/BrunetonsAtmosphere/Textures";

        public Material m_skyMapMaterial;

        public Material m_skyMaterial;

        public Material m_postEffectMaterial;

        public GameObject m_sun;

        public Vector3 m_betaR = new Vector3(0.0058f, 0.0135f, 0.0331f);

        public float m_mieG = 0.75f;

        public float m_sunIntensity = 100.0f;

        private RenderTexture m_skyMap, m_displaySkyMap;

        private Texture2D m_transmittance, m_irradiance;

        private Texture3D m_inscatter;

        private void Start()
        {

            m_skyMap = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            m_skyMap.filterMode = FilterMode.Trilinear;
            m_skyMap.wrapMode = TextureWrapMode.Clamp;
            m_skyMap.useMipMap = true;
            m_skyMap.Create();

            m_displaySkyMap = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            m_displaySkyMap.filterMode = FilterMode.Trilinear;
            m_displaySkyMap.wrapMode = TextureWrapMode.Clamp;
            m_displaySkyMap.useMipMap = true;
            m_displaySkyMap.Create();

            //NOTE - These raw files will not be included by Unity in the build so you will get a 
            //error saying they are missing. You will need to manually place them in the build folder
            //or change to using a supported format like exr.

            //Transmittance is responsible for the change in the sun color as it moves
            //The raw file is a 2D array of 32 bit floats with a range of 0 to 1
            string path = Application.dataPath + m_filePath + "/transmittance.raw";
            int size = TRANSMITTANCE_WIDTH * TRANSMITTANCE_HEIGHT * TRANSMITTANCE_CHANNELS;

            m_transmittance = new Texture2D(TRANSMITTANCE_WIDTH, TRANSMITTANCE_HEIGHT, TextureFormat.RGBAHalf, false, true);
            m_transmittance.SetPixels(ToColor(LoadRawFile(path, size), TRANSMITTANCE_CHANNELS));
            m_transmittance.Apply();

            path = Application.dataPath + m_filePath + "/irradiance.raw";
            size = IRRADIANCE_WIDTH * IRRADIANCE_HEIGHT * IRRADIANCE_CHANNELS;

            m_irradiance = new Texture2D(IRRADIANCE_WIDTH, IRRADIANCE_HEIGHT, TextureFormat.RGBAHalf, false, true);
            m_irradiance.SetPixels(ToColor(LoadRawFile(path, size), IRRADIANCE_CHANNELS));
            m_irradiance.Apply();

            //Inscatter is responsible for the change in the sky color as the sun moves
            //The raw file is a 4D array of 32 bit floats with a range of 0 to 1.589844
            //As there is not such thing as a 4D texture the data is packed into a 3D texture 
            //and the shader manually performs the sample for the 4th dimension
            path = Application.dataPath + m_filePath + "/inscatter.raw";
            size = INSCATTER_WIDTH * INSCATTER_HEIGHT * INSCATTER_DEPTH * INSCATTER_CHANNELS;

            //Should be linear color space. I presume 3D textures always are.
            m_inscatter = new Texture3D(INSCATTER_WIDTH, INSCATTER_HEIGHT, INSCATTER_DEPTH, TextureFormat.RGBAHalf, false);
            m_inscatter.SetPixels(ToColor(LoadRawFile(path, size), INSCATTER_CHANNELS));
            m_inscatter.Apply();

        }

        private void Update()
        {
            Vector3 pos = Camera.main.transform.position;
            pos.y = 0.0f;

            //centre sky dome at player pos
            transform.localPosition = pos;

            UpdateMat(m_skyMapMaterial);
            UpdateMat(m_skyMaterial);
            UpdateMat(m_postEffectMaterial);

            m_skyMapMaterial.SetFloat("_ApplyHDR", 0);
            Graphics.Blit(null, m_skyMap, m_skyMapMaterial);

            if (m_showSkyMap)
            {
                m_skyMapMaterial.SetFloat("_ApplyHDR", 1);
                Graphics.Blit(null, m_displaySkyMap, m_skyMapMaterial);
            }
        }

        private void OnGUI()
        {
            if (!m_showSkyMap) return;
            GUI.DrawTexture(new Rect(0, 0, 256, 256), m_displaySkyMap);
        }

        public void UpdateMat(Material mat)
        {
            if (mat == null) return;

            mat.SetVector("betaR", m_betaR / SCALE);
            mat.SetFloat("mieG", m_mieG);
            mat.SetTexture("_Transmittance", m_transmittance);
            mat.SetTexture("_Irradiance", m_irradiance);
            mat.SetTexture("_Inscatter", m_inscatter);
            mat.SetTexture("_SkyMap", m_skyMap);
            mat.SetFloat("SUN_INTENSITY", m_sunIntensity);
            mat.SetVector("EARTH_POS", new Vector3(0.0f, 6360010.0f, 0.0f));
            mat.SetVector("SUN_DIR", m_sun.transform.forward * -1.0f);

        }

        private void OnDestroy()
        {
            Destroy(m_displaySkyMap);
            Destroy(m_skyMap);
            Destroy(m_transmittance);
            Destroy(m_inscatter);
            Destroy(m_irradiance);
        }

        private float[] LoadRawFile(string path, int size)
        {
            FileInfo fi = new FileInfo(path);

            if (fi == null)
            {
                Debug.Log("Raw file not found (" + path + ")");
                return null;
            }

            FileStream fs = fi.OpenRead();
            byte[] data = new byte[fi.Length];
            fs.Read(data, 0, (int)fi.Length);
            fs.Close();

            //divide by 4 as there are 4 bytes in a 32 bit float
            if (size > fi.Length / 4)
            {
                Debug.Log("Raw file is not the required size (" + path + ")");
                return null;
            }

            float[] map = new float[size];
            for (int x = 0, i = 0; x < size; x++, i += 4)
            {
                //Convert 4 bytes to 1 32 bit float
                map[x] = System.BitConverter.ToSingle(data, i);
            };

            return map;
        }

        private Color[] ToColor(float[] data, int channels)
        {
            int count = data.Length / channels;
            Color[] col = new Color[count];
            
            for(int i = 0; i < count; i++)
            {
                if (channels > 0) col[i].r = data[i * channels + 0];
                if (channels > 1) col[i].g = data[i * channels + 1];
                if (channels > 2) col[i].b = data[i * channels + 2];
                if (channels > 3) col[i].a = data[i * channels + 3];
            }

            return col;
        }
    }
	
}

