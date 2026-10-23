using System;
using System.Collections.Generic;
using Celeste.Graphics.Rendering.Textures;
using UnityEngine;

namespace Celeste.Unity.Editor
{
    internal sealed class SceneArchiveRangeBatch : IDisposable
    {
        private readonly struct Selection
        {
            internal readonly SceneObservedRoomArchive.Layer Layer;
            internal readonly long Revision;

            internal Selection(SceneObservedRoomArchive.Layer layer)
            {
                Layer = layer;
                Revision = layer.SelectionRevision;
            }
        }

        private List<Selection> last = new(), next = new();
        private readonly ScenePresentationMeshQuadBuffer images = new();
        private readonly SceneTileMesh mesh = new(proceduralImages: true);
        private bool valid;

        internal void Begin() => next.Clear();

        internal void Add(SceneObservedRoomArchive.Layer layer) => next.Add(new Selection(layer));

        internal bool HasSmallPendingSelection(int maximumImages)
        {
            int count = 0;
            foreach (var selection in next)
            {
                int selected = selection.Layer.SelectedCount;
                if (selected > maximumImages - count) return false;
                count += selected;
            }

            return count != 0;
        }

        internal void AppendPendingSelection(ScenePresentationMeshQuadBuffer destination)
        {
            foreach (var selection in next)
                for (int i = 0; i < selection.Layer.SelectedCount; i++)
                {
                    var image = selection.Layer.GetSelected(i);
                    image.ForcePointClamp = true;
                    destination.Add(in image);
                }
        }

        internal void Complete(SceneGpuTextureAtlas pointAtlas,
            Func<SourceTexture2DResource, Texture2D> resolveNative, Shader pointShader)
        {
            bool changed = !valid || last.Count != next.Count;
            if (!changed)
                for (int i = 0; i < next.Count; i++)
                    if (!ReferenceEquals(last[i].Layer, next[i].Layer) || last[i].Revision != next[i].Revision)
                    {
                        changed = true;
                        break;
                    }

            if (changed)
            {
                valid = false;
                images.Clear();
                long requiredCapacity = 0;
                foreach (var selection in next) requiredCapacity += selection.Layer.SelectedCount;
                if (requiredCapacity <= int.MaxValue) images.EnsureCapacity((int)requiredCapacity);
                foreach (var selection in next)
                    for (int i = 0; i < selection.Layer.SelectedCount; i++)
                        images.Add(selection.Layer.GetSelected(i));
                try
                {
                    mesh.Rebuild(images, pointAtlas: pointAtlas, resolveNative: resolveNative,
                        pointShader: pointShader);
                }
                catch
                {
                    mesh.Suspend();
                    throw;
                }

                valid = true;
            }

            var previous = last;
            last = next;
            next = previous;
            next.Clear();
        }

        internal void Draw(Func<SourceTexture2DResource, Material> resolve, SceneGpuTextureAtlas pointAtlas,
            Func<SourceTexture2DResource, Texture2D> resolveNative, Shader pointShader)
        {
            if (!valid || !mesh.RefreshPointAtlas(pointAtlas, resolveNative, pointShader))
            {
                valid = false;
                try
                {
                    mesh.Rebuild(images, pointAtlas: pointAtlas, resolveNative: resolveNative,
                        pointShader: pointShader);
                    if (!mesh.RefreshPointAtlas(pointAtlas, resolveNative, pointShader)) mesh.Rebuild(images);
                }
                catch
                {
                    mesh.Suspend();
                    throw;
                }

                valid = true;
            }

            mesh.Draw(Matrix4x4.identity, resolve);
        }

        internal void Suspend()
        {
            last.Clear();
            next.Clear();
            images.Clear();
            valid = false;
            mesh.Suspend();
        }

        public void Dispose()
        {
            last.Clear();
            next.Clear();
            images.Clear();
            valid = false;
            mesh.Dispose();
        }
    }
}
