namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public sealed class UnityEffectShaderProgram
    {
        public readonly string Key, ShaderResource;
        public readonly UnityEffectShaderStage Vertex, Pixel;

        public UnityEffectShaderProgram(string key, UnityEffectShaderStage vertex, UnityEffectShaderStage pixel)
        {
            Key = key;
            ShaderResource = "Celeste/Effects/Programs/" + key;
            Vertex = vertex;
            Pixel = pixel;
        }
    }
}
