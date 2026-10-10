using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SSX_Library.FileHandlers.LevelFiles.Tricky.PS2
{
    public class objSSFHandler
    {

        public static SSFHandler.CollisonModel LoadModel(SSFHandler.CollisonModel Model, string FolderPath)
        {
            Model.Vertices = new List<Vector4>();
            Model.FaceNormals = new List<Vector4>();
            Model.Index = new List<int>();
            string[] Lines = File.ReadAllLines(FolderPath + Model.MeshPath);

            List<Vector4> Normals = new List<Vector4>();

            List<Faces> Faces = new List<Faces>();

            //Load Model
            for (int a = 0; a < Lines.Length; a++)
            {
                string[] splitLine = Lines[a].Split(' ');
                var Check = splitLine.ToList();
                Check.Remove("");
                splitLine = Check.ToArray();

                if (Lines[a].StartsWith("v "))
                {
                    Vector4 vector3 = new Vector4();
                    vector3.X = float.Parse(splitLine[1], CultureInfo.InvariantCulture.NumberFormat);
                    vector3.Y = float.Parse(splitLine[2], CultureInfo.InvariantCulture.NumberFormat);
                    vector3.Z = float.Parse(splitLine[3], CultureInfo.InvariantCulture.NumberFormat);
                    vector3.W = 1f;
                    Model.Vertices.Add(vector3);
                }

                if (Lines[a].StartsWith("vn "))
                {
                    Vector4 vector3 = new Vector4();
                    vector3.X = float.Parse(splitLine[1], CultureInfo.InvariantCulture.NumberFormat);
                    vector3.Y = float.Parse(splitLine[2], CultureInfo.InvariantCulture.NumberFormat);
                    vector3.Z = float.Parse(splitLine[3], CultureInfo.InvariantCulture.NumberFormat);
                    Normals.Add(vector3);
                }

                if (Lines[a].StartsWith("f "))
                {
                    Faces faces = new Faces();
                    // an OBJ face vertex is "v", "v/vt", "v//vn" or "v/vt/vn" - the normal is the THIRD field when
                    // present. A position-only face ("f 1 2 3") has no normal (Normal*Pos = -1); it's computed from
                    // geometry below. (The old code assumed a normal was always present and indexed past the end.)
                    faces.Normal1Pos = ParseFaceVert(splitLine[1], out faces.V1Pos);
                    faces.Normal2Pos = ParseFaceVert(splitLine[2], out faces.V2Pos);
                    faces.Normal3Pos = ParseFaceVert(splitLine[3], out faces.V3Pos);
                    Faces.Add(faces);
                }
            }

            for (int i = 0; i < Faces.Count; i++)
            {
                var TempFace = Faces[i];

                // Use the stored per-face normal when the OBJ supplied one and it's in range; otherwise compute it
                // from the face's own vertices (a position-only collision face). Guards the old unconditional
                // Normals[Normal1Pos] that threw on faces without an explicit normal.
                Vector4 n = (TempFace.Normal1Pos >= 0 && TempFace.Normal1Pos < Normals.Count)
                    ? Normals[TempFace.Normal1Pos]
                    : CalculateFaceNormal(V3(Model.Vertices, TempFace.V1Pos), V3(Model.Vertices, TempFace.V2Pos), V3(Model.Vertices, TempFace.V3Pos));
                Model.FaceNormals.Add(n);

                Model.Index.Add(TempFace.V1Pos);
                Model.Index.Add(TempFace.V2Pos);
                Model.Index.Add(TempFace.V3Pos);
            }



            return Model;
        }

        // Parse one OBJ face vertex ("v", "v/vt", "v//vn", "v/vt/vn"): out the 0-based vertex index, return the
        // 0-based normal index (the third field) or -1 when the face carries no normal.
        static int ParseFaceVert(string token, out int vpos)
        {
            string[] sp = token.Split('/');
            vpos = int.Parse(sp[0]) - 1;
            if (sp.Length >= 3 && sp[2].Length > 0) return int.Parse(sp[2]) - 1;
            return -1;
        }

        // A Vector3 from the model's Vector4 vertex list (bounds-guarded), for the geometry-normal fallback.
        static Vector3 V3(List<Vector4> verts, int i)
        {
            if (i < 0 || i >= verts.Count) return Vector3.Zero;
            var v = verts[i];
            return new Vector3(v.X, v.Y, v.Z);
        }


        public static Vector4 CalculateFaceNormal(Vector3 P1, Vector3 P2, Vector3 P3)
        {
            Vector3 U = P2 - P1;
            Vector3 V = P3 - P1;

            Vector4 Normal = new Vector4();

            var Temp = Vector3.Cross(U, V);

            Temp = Vector3.Normalize(Temp);

            Normal.X = Temp.X;
            Normal.Y = Temp.Y;
            Normal.Z = Temp.Z;

            return Normal;
        }

    }
}
