using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;


namespace BrunetonsOcean
{

	public class ProjectedGrid : MonoBehaviour
    {

        public Material m_oceanMaterial;

        private GameObject m_grid;

        private Projection m_projection;

		void Start()
		{
            m_projection = new Projection();
            m_projection.OceanLevel = transform.position.y;
            m_projection.MaxHeight = 10.0f;

            CreateGrid(8);
        }

        void Update()
        {
          
            Camera cam = Camera.main;
            if (cam == null || m_oceanMaterial == null) return;

            m_projection.OceanLevel = transform.position.y;
            m_projection.MaxHeight = 10.0f;

            m_projection.UpdateProjection(cam);

            m_oceanMaterial.SetMatrix("_Interpolation", m_projection.Interpolation);

            //Once the camera goes below the projection plane (the ocean level) the projected
            //grid will flip the triangle winding order. 
            //Need to flip culling so the top remains the top.
            bool isFlipped = m_projection.IsFlipped;
            m_oceanMaterial.SetInt("_CullFace", (isFlipped) ? (int)CullMode.Front : (int)CullMode.Back);

        }

        /// <summary>
        /// Creates the ocean mesh gameobject.
        /// The resolutions is how many pixels per quad in mesh.
        /// The higher the number the less verts in mesh.
        /// </summary>
        void CreateGrid(int resolution)
        {

            int width = Screen.width;
            int height = Screen.height;
            int numVertsX = width / resolution;
            int numVertsY = height / resolution;

            Mesh mesh = CreateQuad(numVertsX, numVertsY);

            if (mesh == null) return;

            //The position of the mesh is not known until its projected in the shader. 
            //Make the bounds large enough so the camera will draw it.
            float bigNumber = 1e6f;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(bigNumber, 20.0f, bigNumber));

            m_grid = new GameObject("Ocean mesh");
            m_grid.transform.parent = transform;

            MeshFilter filter = m_grid.AddComponent<MeshFilter>();
            MeshRenderer renderer = m_grid.AddComponent<MeshRenderer>();

            filter.sharedMesh = mesh;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.sharedMaterial = m_oceanMaterial;

        }

		public Mesh CreateQuad(int numVertsX, int numVertsY)
		{
			
			Vector3[] vertices = new Vector3[numVertsX * numVertsY];
			Vector2[] texcoords = new Vector2[numVertsX * numVertsY];
			int[] indices = new int[numVertsX * numVertsY * 6];
			
			for (int x = 0; x < numVertsX; x++)
			{
				for (int y = 0; y < numVertsY; y++)
				{
                    Vector2 uv = new Vector3(x / (numVertsX - 1.0f), y / (numVertsY - 1.0f));

                    texcoords[x + y * numVertsX] = uv;
				    vertices[x + y * numVertsX] = new Vector3(uv.x, uv.y, 0.0f);
				}
			}
			
			int num = 0;
			for (int x = 0; x < numVertsX - 1; x++)
			{
				for (int y = 0; y < numVertsY - 1; y++)
				{
					indices[num++] = x + y * numVertsX;
					indices[num++] = x + (y + 1) * numVertsX;
					indices[num++] = (x + 1) + y * numVertsX;
					
					indices[num++] = x + (y + 1) * numVertsX;
					indices[num++] = (x + 1) + (y + 1) * numVertsX;
					indices[num++] = (x + 1) + y * numVertsX;
				}
			}

            if (vertices.Length > 65000)
            {
                //Too many verts to make a mesh. 
                //You will need to split the mesh.
                return null;
            }
            else
            {
                Mesh mesh = new Mesh();
                mesh.vertices = vertices;
                mesh.uv = texcoords;
                mesh.triangles = indices;

                return mesh;
            }
		}

    }


}







