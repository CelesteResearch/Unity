using UnityEngine;

namespace Celeste.Unity.Graphics.SourceDevice.Geometry
{
    public interface IUnitySourceGeometrySubmission
    {
        // Submit all pending commands before mutating a Mesh already referenced by them.
        void BeforeGeometryMutation();
        ulong SubmissionVersion { get; }
        bool Submit(Mesh mesh, int subMesh);
    }
}
