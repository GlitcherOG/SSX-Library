using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace SSXLibrary.FileHandlers.LevelFiles.Tricky.Xbox
{
    public class objTriXBDHandler
    {
        public static Mesh GenerateFaces(List<int> Strips, List<int> Indexs, VertexData vertexData)
        {
            var newMesh = new Mesh();
            newMesh.meshFaces = new List<Faces>();

            int StripStart = 0;
            for (int i = 0; i < Strips.Count; i++)
            {
                //Strips are stitched with repeated indices that don't keep the winding, so the rotation restarts after each join
                int RotationStart = 2;
                bool Joined = true;
                for (int b = 2; b < Strips[i]; b++)
                {
                    int Index = StripStart + b;
                    if (IsDegenerate(Indexs[Index - 2], Indexs[Index - 1], Indexs[Index]))
                    {
                        Joined = true;
                        continue;
                    }

                    if (Joined)
                    {
                        RotationStart = b;
                        Joined = false;
                    }

                    newMesh.meshFaces.Add(CreateFaces(Index, Indexs, vertexData, (b - RotationStart) % 2));
                }
                StripStart += Strips[i];
            }
            return newMesh;
        }

        public static bool IsDegenerate(int Index1, int Index2, int Index3)
        {
            return Index1 == Index2 || Index2 == Index3 || Index1 == Index3;
        }

        public static Faces CreateFaces(int Index, List<int> Indexs, VertexData vertexData, int roatation)
        {
            Faces face = new Faces();
            int Index1 = 0;
            int Index2 = 0;
            int Index3 = 0;
            //Fixes the Rotation For Exporting
            //1-Clockwise
            //0-Counter Clocwise
            if (roatation == 1)
            {
                Index1 = Index;
                Index2 = Index - 1;
                Index3 = Index - 2;
            }
            if (roatation == 0)
            {
                Index1 = Index - 2;
                Index2 = Index - 1;
                Index3 = Index;
            }

            //Positions into the shared vertex buffer
            Index1 = Indexs[Index1];
            Index2 = Indexs[Index2];
            Index3 = Indexs[Index3];

            var Point1 = vertexData.pointDatas[Index1];
            var Point2 = vertexData.pointDatas[Index2];
            var Point3 = vertexData.pointDatas[Index3];

            face.V1 = Point1.vector3s;
            face.V2 = Point2.vector3s;
            face.V3 = Point3.vector3s;

            face.V1Pos = Index1;
            face.V2Pos = Index2;
            face.V3Pos = Index3;

            face.UV1 = Point1.uvs;
            face.UV2 = Point2.uvs;
            face.UV3 = Point3.uvs;

            face.UV1Pos = Index1;
            face.UV2Pos = Index2;
            face.UV3Pos = Index3;

            face.Normal1 = Point1.normals;
            face.Normal2 = Point2.normals;
            face.Normal3 = Point3.normals;

            face.Normal1Pos = Index1;
            face.Normal2Pos = Index2;
            face.Normal3Pos = Index3;

            face.tripstriped = true;

            return face;
        }
    }

    public struct Mesh
    {
        public List<Faces> meshFaces;
    }
    public struct Faces
    {
        public Vector3 V1;
        public Vector3 V2;
        public Vector3 V3;

        public int V1Pos;
        public int V2Pos;
        public int V3Pos;

        public Vector2 UV1;
        public Vector2 UV2;
        public Vector2 UV3;

        public int UV1Pos;
        public int UV2Pos;
        public int UV3Pos;

        public Vector3 Normal1;
        public Vector3 Normal2;
        public Vector3 Normal3;

        public int Normal1Pos;
        public int Normal2Pos;
        public int Normal3Pos;

        public bool tripstriped;
    }
}
