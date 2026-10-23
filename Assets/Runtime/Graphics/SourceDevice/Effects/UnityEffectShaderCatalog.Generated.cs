namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public static partial class UnityEffectShaderCatalog
    {
        public static readonly UnityEffectShaderStage[] Stages =
        {
            new UnityEffectShaderStage("00d867a19d784fd2c1af60bb4901d622be3b29d13b57973151410994e5d6be7a", 2, 400,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96), new UnityEffectUniformPacking(0, 7, 1, 112),
                    new UnityEffectUniformPacking(0, 8, 1, 128), new UnityEffectUniformPacking(0, 9, 1, 144),
                    new UnityEffectUniformPacking(0, 10, 1, 160), new UnityEffectUniformPacking(0, 11, 1, 176),
                    new UnityEffectUniformPacking(0, 12, 1, 192), new UnityEffectUniformPacking(0, 13, 1, 208),
                    new UnityEffectUniformPacking(0, 14, 1, 224), new UnityEffectUniformPacking(0, 15, 1, 240),
                    new UnityEffectUniformPacking(0, 16, 1, 256), new UnityEffectUniformPacking(0, 17, 1, 272),
                    new UnityEffectUniformPacking(0, 18, 1, 288), new UnityEffectUniformPacking(0, 19, 1, 304),
                    new UnityEffectUniformPacking(0, 20, 1, 320), new UnityEffectUniformPacking(0, 21, 1, 336),
                    new UnityEffectUniformPacking(0, 23, 1, 352), new UnityEffectUniformPacking(0, 24, 1, 368),
                    new UnityEffectUniformPacking(0, 25, 1, 384)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(3, 0, 3, 0),
                    new UnityEffectShaderInput(10, 0, 10, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("01732c7e8436573ed6fe317f79dc7746745f29922aacc79968f76298b72e1d16", 2, 128,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96), new UnityEffectUniformPacking(0, 7, 1, 112)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(10, 0, 10, 0), new UnityEffectShaderInput(5, 0, 5, 0),
                    new UnityEffectShaderInput(0, 0, 0, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("04ba79ea63532016364ad2346e801dbf7c9ca0b19ba8d94db14d445c8d9ca6c9", 1, 64,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("0b6183ddb3104e5e5c5ffaaa589819c45c6a3058b8573745085d2e1174de8737", 2, 80,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 15, 1, 16),
                    new UnityEffectUniformPacking(0, 16, 1, 32), new UnityEffectUniformPacking(0, 17, 1, 48),
                    new UnityEffectUniformPacking(0, 18, 1, 64)
                }, new UnityEffectShaderInput[] { new UnityEffectShaderInput(0, 0, 0, 0) },
                new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("1289f06b63de40307f8d3f3a0460542d159dbd82b272facc4352e2e8867bbbb2", 1, 48,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("12e0d81f215828ab134880dceea3f59200e1db8a89d003d924993186185773c8", 1, 16,
                new UnityEffectUniformPacking[] { new UnityEffectUniformPacking(0, 0, 1, 0) },
                new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                {
                    new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0"),
                    new UnityEffectShaderTextureBinding(1, 1, 0, "_CelestePSTex1", "sampler_CelestePSTex1")
                }, 1u),
            new UnityEffectShaderStage("14c9697b45584ef456261a12882e2feaafe6abb0d39d0ea42e75ab3c9314f1fa", 1, 0,
                new UnityEffectUniformPacking[] { }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[] { }, 1u),
            new UnityEffectShaderStage("15bc2b9917baf3027e7dad0c9022559591056337844aa5ac8feec209e6a128d9", 1, 0,
                new UnityEffectUniformPacking[] { }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("1c6cc123966d647a4f5f4557a9944da6be4c4841a33b01d590c4c29072e8b847", 1, 0,
                new UnityEffectUniformPacking[] { }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("2136cd2c9fc8e50e237e2d589ea9793eec71d527a002d5be2bc8a019667240b4", 2, 96,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 14, 1, 16),
                    new UnityEffectUniformPacking(0, 15, 1, 32), new UnityEffectUniformPacking(0, 16, 1, 48),
                    new UnityEffectUniformPacking(0, 17, 1, 64), new UnityEffectUniformPacking(0, 18, 1, 80)
                },
                new UnityEffectShaderInput[]
                    { new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(10, 0, 10, 0) },
                new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("29533b27235aa2b733720695baec04b78fef1b537d06f4c1723896e277b78f79", 1, 64,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("2f850a85de7eff871533c13b8bfb75c3ad130bfeb2820086a45f82f915466537", 1, 0,
                new UnityEffectUniformPacking[] { }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("33612e18009a0dbdb0a2c1647680dfe3635b2de52671700bb68f8844abd88812", 2, 304,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96), new UnityEffectUniformPacking(0, 13, 1, 112),
                    new UnityEffectUniformPacking(0, 14, 1, 128), new UnityEffectUniformPacking(0, 15, 1, 144),
                    new UnityEffectUniformPacking(0, 16, 1, 160), new UnityEffectUniformPacking(0, 17, 1, 176),
                    new UnityEffectUniformPacking(0, 18, 1, 192), new UnityEffectUniformPacking(0, 19, 1, 208),
                    new UnityEffectUniformPacking(0, 20, 1, 224), new UnityEffectUniformPacking(0, 21, 1, 240),
                    new UnityEffectUniformPacking(0, 23, 1, 256), new UnityEffectUniformPacking(0, 24, 1, 272),
                    new UnityEffectUniformPacking(0, 25, 1, 288)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(3, 0, 3, 0),
                    new UnityEffectShaderInput(5, 0, 5, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("3744f6e236b1ce46fcc968de182cf7850b53a25fd7dabddf22890ecd2d7c7bce", 1, 80,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("3a958654dd3abf2cf36ffb45926a6df48a24c7d875a17c6a8cfba0b3511a691f", 1, 176,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96), new UnityEffectUniformPacking(0, 7, 1, 112),
                    new UnityEffectUniformPacking(0, 8, 1, 128), new UnityEffectUniformPacking(0, 9, 1, 144),
                    new UnityEffectUniformPacking(0, 10, 1, 160)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 7u),
            new UnityEffectShaderStage("3b3892309cc467b2db7f4e8e122d4b9812002ebbcf8aa7fd5f7b6ddc55c47926", 2, 400,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96), new UnityEffectUniformPacking(0, 7, 1, 112),
                    new UnityEffectUniformPacking(0, 8, 1, 128), new UnityEffectUniformPacking(0, 9, 1, 144),
                    new UnityEffectUniformPacking(0, 10, 1, 160), new UnityEffectUniformPacking(0, 11, 1, 176),
                    new UnityEffectUniformPacking(0, 12, 1, 192), new UnityEffectUniformPacking(0, 13, 1, 208),
                    new UnityEffectUniformPacking(0, 14, 1, 224), new UnityEffectUniformPacking(0, 15, 1, 240),
                    new UnityEffectUniformPacking(0, 16, 1, 256), new UnityEffectUniformPacking(0, 17, 1, 272),
                    new UnityEffectUniformPacking(0, 18, 1, 288), new UnityEffectUniformPacking(0, 19, 1, 304),
                    new UnityEffectUniformPacking(0, 20, 1, 320), new UnityEffectUniformPacking(0, 21, 1, 336),
                    new UnityEffectUniformPacking(0, 23, 1, 352), new UnityEffectUniformPacking(0, 24, 1, 368),
                    new UnityEffectUniformPacking(0, 25, 1, 384)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(3, 0, 3, 0),
                    new UnityEffectShaderInput(5, 0, 5, 0), new UnityEffectShaderInput(10, 0, 10, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("3cebf1e9ebce326fe7a798d94097b16aca7b0a3c1962f5aac7b7e99088bbb0b4", 1, 32,
                new UnityEffectUniformPacking[]
                    { new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16) },
                new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                {
                    new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0"),
                    new UnityEffectShaderTextureBinding(1, 1, 0, "_CelestePSTex1", "sampler_CelestePSTex1")
                }, 1u),
            new UnityEffectShaderStage("406281435c0fbbf10563f2fc06a73c8f8999db1849e314acc5a6b21f34cac912", 1, 48,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                {
                    new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0"),
                    new UnityEffectShaderTextureBinding(1, 1, 0, "_CelestePSTex1", "sampler_CelestePSTex1")
                }, 1u),
            new UnityEffectShaderStage("475088293e24a5bff3ff0f7a6a0f163072ab42330c57f38d6bc6cde1220c6ade", 1, 16,
                new UnityEffectUniformPacking[] { new UnityEffectUniformPacking(0, 0, 1, 0) },
                new UnityEffectShaderInput[] { }, new UnityEffectShaderTextureBinding[] { }, 1u),
            new UnityEffectShaderStage("48addd89417ea1fbf74f705e23e34eaf649c956edfab779d0e7bda16bffc4730", 1, 96,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 6, 1, 48),
                    new UnityEffectUniformPacking(0, 7, 1, 64), new UnityEffectUniformPacking(0, 8, 1, 80)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("49691728502505dc348f85dfd01c0c2de96a3c90124e07218834da5453081313", 1, 80,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 3, 0, "_CelestePSTex3", "sampler_CelestePSTex3") }, 1u),
            new UnityEffectShaderStage("4c769fd0a33a65093b2bb5364e46cf9d2cde5ef1135ccc6b347c6d6a2993c4ee", 2, 400,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96), new UnityEffectUniformPacking(0, 7, 1, 112),
                    new UnityEffectUniformPacking(0, 8, 1, 128), new UnityEffectUniformPacking(0, 9, 1, 144),
                    new UnityEffectUniformPacking(0, 10, 1, 160), new UnityEffectUniformPacking(0, 11, 1, 176),
                    new UnityEffectUniformPacking(0, 12, 1, 192), new UnityEffectUniformPacking(0, 13, 1, 208),
                    new UnityEffectUniformPacking(0, 14, 1, 224), new UnityEffectUniformPacking(0, 15, 1, 240),
                    new UnityEffectUniformPacking(0, 16, 1, 256), new UnityEffectUniformPacking(0, 17, 1, 272),
                    new UnityEffectUniformPacking(0, 18, 1, 288), new UnityEffectUniformPacking(0, 19, 1, 304),
                    new UnityEffectUniformPacking(0, 20, 1, 320), new UnityEffectUniformPacking(0, 21, 1, 336),
                    new UnityEffectUniformPacking(0, 23, 1, 352), new UnityEffectUniformPacking(0, 24, 1, 368),
                    new UnityEffectUniformPacking(0, 25, 1, 384)
                },
                new UnityEffectShaderInput[]
                    { new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(3, 0, 3, 0) },
                new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("4e0e94bae6915011301b7d8d85a299742c9da327a79753fbf2d0d1266f685d77", 1, 0,
                new UnityEffectUniformPacking[] { }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("5077c5529980f7077003edf4120988289c794ebd1ccb1aede04287e377ab71d5", 2, 144,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96), new UnityEffectUniformPacking(0, 7, 1, 112),
                    new UnityEffectUniformPacking(0, 8, 1, 128)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(5, 0, 5, 0), new UnityEffectShaderInput(5, 1, 5, 1),
                    new UnityEffectShaderInput(5, 2, 5, 2)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("53f87fec9bff02e29d5c1b3086a8c2a697d72bdfa5d8177997b807a8aefc272d", 2, 96,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 14, 1, 16),
                    new UnityEffectUniformPacking(0, 15, 1, 32), new UnityEffectUniformPacking(0, 16, 1, 48),
                    new UnityEffectUniformPacking(0, 17, 1, 64), new UnityEffectUniformPacking(0, 18, 1, 80)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(5, 0, 5, 0),
                    new UnityEffectShaderInput(10, 0, 10, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("59f81cdc19247928f7ea8275fe8fc9ac950feff75aabc286ef9e935f742fe46a", 2, 192,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 14, 1, 16),
                    new UnityEffectUniformPacking(0, 15, 1, 32), new UnityEffectUniformPacking(0, 16, 1, 48),
                    new UnityEffectUniformPacking(0, 17, 1, 64), new UnityEffectUniformPacking(0, 18, 1, 80),
                    new UnityEffectUniformPacking(0, 19, 1, 96), new UnityEffectUniformPacking(0, 20, 1, 112),
                    new UnityEffectUniformPacking(0, 21, 1, 128), new UnityEffectUniformPacking(0, 23, 1, 144),
                    new UnityEffectUniformPacking(0, 24, 1, 160), new UnityEffectUniformPacking(0, 25, 1, 176)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(3, 0, 3, 0),
                    new UnityEffectShaderInput(5, 0, 5, 0), new UnityEffectShaderInput(10, 0, 10, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("6713959ef29cc6ac1dfdbb72e9aa88c7f36177314ef2dcd553bb4e0a6ec199e3", 2, 304,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96), new UnityEffectUniformPacking(0, 13, 1, 112),
                    new UnityEffectUniformPacking(0, 14, 1, 128), new UnityEffectUniformPacking(0, 15, 1, 144),
                    new UnityEffectUniformPacking(0, 16, 1, 160), new UnityEffectUniformPacking(0, 17, 1, 176),
                    new UnityEffectUniformPacking(0, 18, 1, 192), new UnityEffectUniformPacking(0, 19, 1, 208),
                    new UnityEffectUniformPacking(0, 20, 1, 224), new UnityEffectUniformPacking(0, 21, 1, 240),
                    new UnityEffectUniformPacking(0, 23, 1, 256), new UnityEffectUniformPacking(0, 24, 1, 272),
                    new UnityEffectUniformPacking(0, 25, 1, 288)
                },
                new UnityEffectShaderInput[]
                    { new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(3, 0, 3, 0) },
                new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("6a1ee948751ac4e37db9cd5ef392708114e7ec4587d1afa7c1ce4afb434e512e", 1, 16,
                new UnityEffectUniformPacking[] { new UnityEffectUniformPacking(0, 0, 1, 0) },
                new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                {
                    new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0"),
                    new UnityEffectShaderTextureBinding(1, 1, 0, "_CelestePSTex1", "sampler_CelestePSTex1"),
                    new UnityEffectShaderTextureBinding(1, 2, 0, "_CelestePSTex2", "sampler_CelestePSTex2")
                }, 1u),
            new UnityEffectShaderStage("6b624026dec12fd88e004a2f818ec2d3203af24beedfbae0c1f0d2cc08d4b22c", 1, 0,
                new UnityEffectUniformPacking[] { }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[] { }, 1u),
            new UnityEffectShaderStage("6c5ff8087783fa6d78716ee388d6b52df760dfc58c959e67e457dbf5bd46d3e1", 1, 0,
                new UnityEffectUniformPacking[] { }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("6e2e10eca8d138e36aded8ba935ffe29e79d6c54ec393500b6541f57620805ff", 1, 112,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("70ab1173112c7af0cbd7496e8375707d9606d4bd623cb4f66bb2fc6e4aca6cc0", 2, 64,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48)
                },
                new UnityEffectShaderInput[]
                    { new UnityEffectShaderInput(10, 0, 10, 0), new UnityEffectShaderInput(0, 0, 0, 0) },
                new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("7111985637efbec35b156943c4f492f9d1d8e04e8d9612cada505676626791c7", 1, 112,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                {
                    new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0"),
                    new UnityEffectShaderTextureBinding(1, 1, 0, "_CelestePSTex1", "sampler_CelestePSTex1"),
                    new UnityEffectShaderTextureBinding(1, 2, 0, "_CelestePSTex2", "sampler_CelestePSTex2")
                }, 1u),
            new UnityEffectShaderStage("71a8877d11bde7429417a6fe877479e9727feadd93bd2b0d6bfa3f0a12dc1933", 1, 48,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                {
                    new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0"),
                    new UnityEffectShaderTextureBinding(1, 1, 0, "_CelestePSTex1", "sampler_CelestePSTex1")
                }, 1u),
            new UnityEffectShaderStage("74acbb85a2b9d69a3d04f06933de2da29e6150901986b083673925c1d7602fff", 1, 16,
                new UnityEffectUniformPacking[] { new UnityEffectUniformPacking(0, 0, 1, 0) },
                new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("768bd0e8fe45db41345657096bb6fb9314991964c17fac7ecd547d10da9370ed", 2, 96,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 14, 1, 16),
                    new UnityEffectUniformPacking(0, 15, 1, 32), new UnityEffectUniformPacking(0, 16, 1, 48),
                    new UnityEffectUniformPacking(0, 17, 1, 64), new UnityEffectUniformPacking(0, 18, 1, 80)
                }, new UnityEffectShaderInput[] { new UnityEffectShaderInput(0, 0, 0, 0) },
                new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("792321c0f2b9282da721ba75b6957d92eca9f9a7a53b4130635083e819f09b10", 1, 16,
                new UnityEffectUniformPacking[] { new UnityEffectUniformPacking(0, 0, 1, 0) },
                new UnityEffectShaderInput[] { }, new UnityEffectShaderTextureBinding[] { }, 1u),
            new UnityEffectShaderStage("792b7bc01a9442f6056181a3202e3849dfe4f6bc36a27ad765376d8b99819c7c", 2, 64,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48)
                },
                new UnityEffectShaderInput[]
                    { new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(10, 0, 10, 0) },
                new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("7a8d04925a8c76aaa4e867871c8433668755f4798fe7b00e69fbefe7a0190be8", 2, 64,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48)
                },
                new UnityEffectShaderInput[]
                    { new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(5, 0, 5, 0) },
                new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("87ecce13df98431d475749066e30d321363429989281a1f889ec319766b17226", 1, 0,
                new UnityEffectUniformPacking[] { }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                {
                    new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0"),
                    new UnityEffectShaderTextureBinding(1, 1, 0, "_CelestePSTex1", "sampler_CelestePSTex1")
                }, 1u),
            new UnityEffectShaderStage("89bbd6f1d8681b90fe6d5b1e600f64adb215ddfeaf76c18995d5966f6facf137", 1, 0,
                new UnityEffectUniformPacking[] { }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[] { }, 1u),
            new UnityEffectShaderStage("8ce9aa73826b1ff569ff572266469772e244054be5c183fef10f8c39e9946257", 2, 64,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(10, 0, 10, 0),
                    new UnityEffectShaderInput(5, 0, 5, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("8e7f2d56f8db695913d9b97a521f99dc2fc597f758a43583d62f71856836418b", 2, 80,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 15, 1, 16),
                    new UnityEffectUniformPacking(0, 16, 1, 32), new UnityEffectUniformPacking(0, 17, 1, 48),
                    new UnityEffectUniformPacking(0, 18, 1, 64)
                },
                new UnityEffectShaderInput[]
                    { new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(10, 0, 10, 0) },
                new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("8ed144b63c9944f8396fd2392cd613cc9bcbc85f666072672581af4a363585fa", 1, 240,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96), new UnityEffectUniformPacking(0, 7, 1, 112),
                    new UnityEffectUniformPacking(0, 8, 1, 128), new UnityEffectUniformPacking(0, 9, 1, 144),
                    new UnityEffectUniformPacking(0, 10, 1, 160), new UnityEffectUniformPacking(0, 11, 1, 176),
                    new UnityEffectUniformPacking(0, 12, 1, 192), new UnityEffectUniformPacking(0, 13, 1, 208),
                    new UnityEffectUniformPacking(0, 14, 1, 224)
                }, new UnityEffectShaderInput[] { }, new UnityEffectShaderTextureBinding[] { }, 1u),
            new UnityEffectShaderStage("8f3993aa81d137c99de1b3202de8feccdd43077275f9a63c89557b1a95076d13", 2, 192,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 14, 1, 16),
                    new UnityEffectUniformPacking(0, 15, 1, 32), new UnityEffectUniformPacking(0, 16, 1, 48),
                    new UnityEffectUniformPacking(0, 17, 1, 64), new UnityEffectUniformPacking(0, 18, 1, 80),
                    new UnityEffectUniformPacking(0, 19, 1, 96), new UnityEffectUniformPacking(0, 20, 1, 112),
                    new UnityEffectUniformPacking(0, 21, 1, 128), new UnityEffectUniformPacking(0, 23, 1, 144),
                    new UnityEffectUniformPacking(0, 24, 1, 160), new UnityEffectUniformPacking(0, 25, 1, 176)
                },
                new UnityEffectShaderInput[]
                    { new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(3, 0, 3, 0) },
                new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("93e00e6f4bc0ca3d1b6fded1bf37fa29c6aadab3e9f0a173e34b36065cd5bd12", 1, 32,
                new UnityEffectUniformPacking[]
                    { new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16) },
                new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("997c1335f62ba2786a270a97d6858b829e4aed42ea6fdeb6e86b861b5d704a70", 1, 112,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                {
                    new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0"),
                    new UnityEffectShaderTextureBinding(1, 1, 0, "_CelestePSTex1", "sampler_CelestePSTex1"),
                    new UnityEffectShaderTextureBinding(1, 2, 0, "_CelestePSTex2", "sampler_CelestePSTex2")
                }, 1u),
            new UnityEffectShaderStage("9b880f2555ae7e0c40edcafed92c38512e1dcac46c566fc51bf96930461fa9ca", 2, 64,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(10, 0, 10, 0), new UnityEffectShaderInput(5, 0, 5, 0),
                    new UnityEffectShaderInput(0, 0, 0, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("a17a06fb4989fbbaed6543c47394b15ed8e3ecab33399cb8a8e2fe8195744c42", 1, 192,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96), new UnityEffectUniformPacking(0, 7, 1, 112),
                    new UnityEffectUniformPacking(0, 8, 1, 128), new UnityEffectUniformPacking(0, 9, 1, 144),
                    new UnityEffectUniformPacking(0, 10, 1, 160), new UnityEffectUniformPacking(0, 11, 1, 176)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 3u),
            new UnityEffectShaderStage("a32ef72449db376d624cc2cc9b50db6678374b37df4b969935393bad1ab26df9", 1, 0,
                new UnityEffectUniformPacking[] { }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[] { }, 1u),
            new UnityEffectShaderStage("abca5e7765f40b60164a1efa16fa54093e6a8e1ca1ebb6667df87c5197b90d43", 1, 480,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 2, 1, 16),
                    new UnityEffectUniformPacking(0, 3, 1, 32), new UnityEffectUniformPacking(0, 4, 1, 48),
                    new UnityEffectUniformPacking(0, 5, 1, 64), new UnityEffectUniformPacking(0, 6, 1, 80),
                    new UnityEffectUniformPacking(0, 7, 1, 96), new UnityEffectUniformPacking(0, 8, 1, 112),
                    new UnityEffectUniformPacking(0, 9, 1, 128), new UnityEffectUniformPacking(0, 10, 1, 144),
                    new UnityEffectUniformPacking(0, 11, 1, 160), new UnityEffectUniformPacking(0, 12, 1, 176),
                    new UnityEffectUniformPacking(0, 13, 1, 192), new UnityEffectUniformPacking(0, 14, 1, 208),
                    new UnityEffectUniformPacking(0, 15, 1, 224), new UnityEffectUniformPacking(0, 16, 1, 240),
                    new UnityEffectUniformPacking(0, 17, 1, 256), new UnityEffectUniformPacking(0, 18, 1, 272),
                    new UnityEffectUniformPacking(0, 19, 1, 288), new UnityEffectUniformPacking(0, 20, 1, 304),
                    new UnityEffectUniformPacking(0, 21, 1, 320), new UnityEffectUniformPacking(0, 22, 1, 336),
                    new UnityEffectUniformPacking(0, 23, 1, 352), new UnityEffectUniformPacking(0, 24, 1, 368),
                    new UnityEffectUniformPacking(0, 25, 1, 384), new UnityEffectUniformPacking(0, 26, 1, 400),
                    new UnityEffectUniformPacking(0, 27, 1, 416), new UnityEffectUniformPacking(0, 28, 1, 432),
                    new UnityEffectUniformPacking(0, 29, 1, 448), new UnityEffectUniformPacking(0, 30, 1, 464)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 2, 0, "_CelestePSTex2", "sampler_CelestePSTex2") }, 1u),
            new UnityEffectShaderStage("aed811a7dbab0269b6c365d85a0eb4568e2fc822f3b0ce405a8f5f9014f0107a", 1, 16,
                new UnityEffectUniformPacking[] { new UnityEffectUniformPacking(0, 0, 1, 0) },
                new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                {
                    new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0"),
                    new UnityEffectShaderTextureBinding(1, 1, 0, "_CelestePSTex1", "sampler_CelestePSTex1"),
                    new UnityEffectShaderTextureBinding(1, 2, 0, "_CelestePSTex2", "sampler_CelestePSTex2")
                }, 1u),
            new UnityEffectShaderStage("b05d2f1a793dfb6c89f7aeba2881cb4080f2f3a4e20ef44e8501b9439000f134", 2, 400,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96), new UnityEffectUniformPacking(0, 7, 1, 112),
                    new UnityEffectUniformPacking(0, 8, 1, 128), new UnityEffectUniformPacking(0, 9, 1, 144),
                    new UnityEffectUniformPacking(0, 10, 1, 160), new UnityEffectUniformPacking(0, 11, 1, 176),
                    new UnityEffectUniformPacking(0, 12, 1, 192), new UnityEffectUniformPacking(0, 13, 1, 208),
                    new UnityEffectUniformPacking(0, 14, 1, 224), new UnityEffectUniformPacking(0, 15, 1, 240),
                    new UnityEffectUniformPacking(0, 16, 1, 256), new UnityEffectUniformPacking(0, 17, 1, 272),
                    new UnityEffectUniformPacking(0, 18, 1, 288), new UnityEffectUniformPacking(0, 19, 1, 304),
                    new UnityEffectUniformPacking(0, 20, 1, 320), new UnityEffectUniformPacking(0, 21, 1, 336),
                    new UnityEffectUniformPacking(0, 23, 1, 352), new UnityEffectUniformPacking(0, 24, 1, 368),
                    new UnityEffectUniformPacking(0, 25, 1, 384)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(3, 0, 3, 0),
                    new UnityEffectShaderInput(5, 0, 5, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("b44ad89297cb311dbe358eb1e69e219ee5093e9fa3b4489d49d3a365c1ea99b6", 1, 16,
                new UnityEffectUniformPacking[] { new UnityEffectUniformPacking(0, 0, 1, 0) },
                new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("b52b207ddd018f611df11d28b5f6a914dfd3c22f680d18a73ac1cb743444e0dc", 2, 192,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 14, 1, 16),
                    new UnityEffectUniformPacking(0, 15, 1, 32), new UnityEffectUniformPacking(0, 16, 1, 48),
                    new UnityEffectUniformPacking(0, 17, 1, 64), new UnityEffectUniformPacking(0, 18, 1, 80),
                    new UnityEffectUniformPacking(0, 19, 1, 96), new UnityEffectUniformPacking(0, 20, 1, 112),
                    new UnityEffectUniformPacking(0, 21, 1, 128), new UnityEffectUniformPacking(0, 23, 1, 144),
                    new UnityEffectUniformPacking(0, 24, 1, 160), new UnityEffectUniformPacking(0, 25, 1, 176)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(3, 0, 3, 0),
                    new UnityEffectShaderInput(10, 0, 10, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("ba24d8080589d692add267d9789dce14236b2ed8446f24488e7b8126d1708211", 1, 96,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                {
                    new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0"),
                    new UnityEffectShaderTextureBinding(1, 1, 0, "_CelestePSTex1", "sampler_CelestePSTex1")
                }, 1u),
            new UnityEffectShaderStage("bd1034a112107e1374d9449f93ac1ce1b333da1fad30339319c21c3fc08137d5", 2, 192,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 14, 1, 16),
                    new UnityEffectUniformPacking(0, 15, 1, 32), new UnityEffectUniformPacking(0, 16, 1, 48),
                    new UnityEffectUniformPacking(0, 17, 1, 64), new UnityEffectUniformPacking(0, 18, 1, 80),
                    new UnityEffectUniformPacking(0, 19, 1, 96), new UnityEffectUniformPacking(0, 20, 1, 112),
                    new UnityEffectUniformPacking(0, 21, 1, 128), new UnityEffectUniformPacking(0, 23, 1, 144),
                    new UnityEffectUniformPacking(0, 24, 1, 160), new UnityEffectUniformPacking(0, 25, 1, 176)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(3, 0, 3, 0),
                    new UnityEffectShaderInput(5, 0, 5, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("bd7232b597a5cc8dedef974fcc0a10c67769e7432393308dd2145a3ddf5a6979", 1, 240,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96), new UnityEffectUniformPacking(0, 7, 1, 112),
                    new UnityEffectUniformPacking(0, 8, 1, 128), new UnityEffectUniformPacking(0, 9, 1, 144),
                    new UnityEffectUniformPacking(0, 10, 1, 160), new UnityEffectUniformPacking(0, 11, 1, 176),
                    new UnityEffectUniformPacking(0, 12, 1, 192), new UnityEffectUniformPacking(0, 13, 1, 208),
                    new UnityEffectUniformPacking(0, 14, 1, 224)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("c1f56b94e9c188f14a0c0e65243a1a4c4a2b556d9bd98d4b1c2b86f8ddb6b59a", 2, 304,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96), new UnityEffectUniformPacking(0, 13, 1, 112),
                    new UnityEffectUniformPacking(0, 14, 1, 128), new UnityEffectUniformPacking(0, 15, 1, 144),
                    new UnityEffectUniformPacking(0, 16, 1, 160), new UnityEffectUniformPacking(0, 17, 1, 176),
                    new UnityEffectUniformPacking(0, 18, 1, 192), new UnityEffectUniformPacking(0, 19, 1, 208),
                    new UnityEffectUniformPacking(0, 20, 1, 224), new UnityEffectUniformPacking(0, 21, 1, 240),
                    new UnityEffectUniformPacking(0, 23, 1, 256), new UnityEffectUniformPacking(0, 24, 1, 272),
                    new UnityEffectUniformPacking(0, 25, 1, 288)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(3, 0, 3, 0),
                    new UnityEffectShaderInput(10, 0, 10, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("c8cf05c065d6648ae1d9f72690d0fdc5776d821b12e669318cb373f59a91957e", 1, 0,
                new UnityEffectUniformPacking[] { }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                {
                    new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0"),
                    new UnityEffectShaderTextureBinding(1, 1, 0, "_CelestePSTex1", "sampler_CelestePSTex1")
                }, 1u),
            new UnityEffectShaderStage("d0b2f8848600cd0f8870149c6717cb2c00e2a73cb825b093bee840b731348745", 2, 64,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(10, 0, 10, 0),
                    new UnityEffectShaderInput(10, 1, 5, 7), new UnityEffectShaderInput(5, 0, 5, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("d388f9bc9b3beb6295f70ba9625f1d6a15131e3c8f554c1ace2bbac0806a98b2", 2, 96,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(10, 0, 10, 0),
                    new UnityEffectShaderInput(5, 0, 5, 0), new UnityEffectShaderInput(3, 0, 3, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("d46bb7e1bc7056c5c1a9980298a6984e4521016ead06c80997719c442950dc8d", 2, 64,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(10, 0, 10, 0), new UnityEffectShaderInput(5, 0, 5, 0),
                    new UnityEffectShaderInput(0, 0, 0, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("d673c10cceb9037d16b7dfefdfa700a6e2cfb7607f9911dab2535019b084a6fe", 1, 16,
                new UnityEffectUniformPacking[] { new UnityEffectUniformPacking(0, 0, 1, 0) },
                new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("d7e59c7d1ecb16ee501f5c8fabbb65b15b927a0078a726ffdcd7a737a40d5dfe", 1, 0,
                new UnityEffectUniformPacking[] { }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("d962b80f0d94b456f1bc1f5ef0be9d9d9df3d17206fb8f76d40c8b35b90a9b8f", 1, 16,
                new UnityEffectUniformPacking[] { new UnityEffectUniformPacking(0, 0, 1, 0) },
                new UnityEffectShaderInput[] { }, new UnityEffectShaderTextureBinding[] { }, 1u),
            new UnityEffectShaderStage("dabf340f3e8c62c747f98e0c6f98e9fcc286ef15fa043bb84561471a1202df22", 2, 64,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48)
                },
                new UnityEffectShaderInput[]
                    { new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(10, 0, 10, 0) },
                new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("e0636510186c9023b31513838f8b82a6973729761697400821b817f707a442aa", 2, 80,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 15, 1, 16),
                    new UnityEffectUniformPacking(0, 16, 1, 32), new UnityEffectUniformPacking(0, 17, 1, 48),
                    new UnityEffectUniformPacking(0, 18, 1, 64)
                },
                new UnityEffectShaderInput[]
                    { new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(5, 0, 5, 0) },
                new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("e0d2a76d695224aa53dd163011e4c1770236b4efb75e0fc27adf9e8af9275e18", 1, 16,
                new UnityEffectUniformPacking[] { new UnityEffectUniformPacking(0, 0, 1, 0) },
                new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("e14f70b7123e23d818e5d65db7d5085a7b2c567acbb3b9ae73587c594de75954", 1, 16,
                new UnityEffectUniformPacking[] { new UnityEffectUniformPacking(0, 0, 1, 0) },
                new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("e5120d746716edc0391087ea918dacf5f194299fca0080509ecb71861700b668", 1, 0,
                new UnityEffectUniformPacking[] { }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("e57c1005a2a48fd21440b628db32c760ac4b490c53cac30c80f5ca744cf931a3", 2, 96,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 14, 1, 16),
                    new UnityEffectUniformPacking(0, 15, 1, 32), new UnityEffectUniformPacking(0, 16, 1, 48),
                    new UnityEffectUniformPacking(0, 17, 1, 64), new UnityEffectUniformPacking(0, 18, 1, 80)
                },
                new UnityEffectShaderInput[]
                    { new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(5, 0, 5, 0) },
                new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("e83cd2cee6be09d04e1c6a458db799f20d1ce60fa0dd9ec396c4c97b478338b2", 2, 64,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(10, 0, 10, 0),
                    new UnityEffectShaderInput(5, 0, 5, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("eb8f22953f7337cafa6013bd20efc36736c16b883df09dfdaad9a7a0de40c4fc", 2, 304,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96), new UnityEffectUniformPacking(0, 13, 1, 112),
                    new UnityEffectUniformPacking(0, 14, 1, 128), new UnityEffectUniformPacking(0, 15, 1, 144),
                    new UnityEffectUniformPacking(0, 16, 1, 160), new UnityEffectUniformPacking(0, 17, 1, 176),
                    new UnityEffectUniformPacking(0, 18, 1, 192), new UnityEffectUniformPacking(0, 19, 1, 208),
                    new UnityEffectUniformPacking(0, 20, 1, 224), new UnityEffectUniformPacking(0, 21, 1, 240),
                    new UnityEffectUniformPacking(0, 23, 1, 256), new UnityEffectUniformPacking(0, 24, 1, 272),
                    new UnityEffectUniformPacking(0, 25, 1, 288)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(3, 0, 3, 0),
                    new UnityEffectShaderInput(5, 0, 5, 0), new UnityEffectShaderInput(10, 0, 10, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("edeadd3e7e241dbaab1a2447c9aa086cef66a49bcdc376fa919505cfc8749c3b", 1, 16,
                new UnityEffectUniformPacking[] { new UnityEffectUniformPacking(0, 0, 1, 0) },
                new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("ee771cc30449201e508b991a4b719a081f78d2f98c0fa1dd964601a992aad107", 2, 80,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 15, 1, 16),
                    new UnityEffectUniformPacking(0, 16, 1, 32), new UnityEffectUniformPacking(0, 17, 1, 48),
                    new UnityEffectUniformPacking(0, 18, 1, 64)
                },
                new UnityEffectShaderInput[]
                {
                    new UnityEffectShaderInput(0, 0, 0, 0), new UnityEffectShaderInput(5, 0, 5, 0),
                    new UnityEffectShaderInput(10, 0, 10, 0)
                }, new UnityEffectShaderTextureBinding[] { }, 0u),
            new UnityEffectShaderStage("eeb53e5a7410c51140f636b5eb9806e9c4fe67de788210662e1123a50d9537ed", 1, 336,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64), new UnityEffectUniformPacking(0, 5, 1, 80),
                    new UnityEffectUniformPacking(0, 6, 1, 96), new UnityEffectUniformPacking(0, 7, 1, 112),
                    new UnityEffectUniformPacking(0, 8, 1, 128), new UnityEffectUniformPacking(0, 9, 1, 144),
                    new UnityEffectUniformPacking(0, 10, 1, 160), new UnityEffectUniformPacking(0, 11, 1, 176),
                    new UnityEffectUniformPacking(0, 12, 1, 192), new UnityEffectUniformPacking(0, 13, 1, 208),
                    new UnityEffectUniformPacking(0, 14, 1, 224), new UnityEffectUniformPacking(0, 15, 1, 240),
                    new UnityEffectUniformPacking(0, 16, 1, 256), new UnityEffectUniformPacking(0, 17, 1, 272),
                    new UnityEffectUniformPacking(0, 18, 1, 288), new UnityEffectUniformPacking(0, 19, 1, 304),
                    new UnityEffectUniformPacking(0, 20, 1, 320)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                {
                    new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0"),
                    new UnityEffectShaderTextureBinding(1, 1, 0, "_CelestePSTex1", "sampler_CelestePSTex1")
                }, 1u),
            new UnityEffectShaderStage("f141b42211dba1b25e55f6ee5cef2c6b9bc45f5975c7aa72131e455c873b9302", 1, 80,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("f250230dc4e043cd031d8f1705b3831a58ac7c9c701aa694ed147af839564abf", 1, 0,
                new UnityEffectUniformPacking[] { }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                {
                    new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0"),
                    new UnityEffectShaderTextureBinding(1, 1, 0, "_CelestePSTex1", "sampler_CelestePSTex1")
                }, 1u),
            new UnityEffectShaderStage("f4110b4cf5d2403795b9d8f9b1812670bb4dbb1d025fefe91b5d6b73daf9079b", 1, 80,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64)
                }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("f6ad45791228225e28536813f310600ddd21942b0c04d69c0d4ddc132eaa3690", 1, 32,
                new UnityEffectUniformPacking[]
                    { new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16) },
                new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                {
                    new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0"),
                    new UnityEffectShaderTextureBinding(1, 1, 0, "_CelestePSTex1", "sampler_CelestePSTex1")
                }, 1u),
            new UnityEffectShaderStage("fa50467164b4ff08075b534f42facc10d6b2ad74f9d03676c4c4dac869241d7e", 1, 80,
                new UnityEffectUniformPacking[]
                {
                    new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16),
                    new UnityEffectUniformPacking(0, 2, 1, 32), new UnityEffectUniformPacking(0, 3, 1, 48),
                    new UnityEffectUniformPacking(0, 4, 1, 64)
                }, new UnityEffectShaderInput[] { }, new UnityEffectShaderTextureBinding[] { }, 1u),
            new UnityEffectShaderStage("fb70f0c0cca4113bdd478b4d604e82d0462a9794638cc183e688d1f0df771004", 1, 0,
                new UnityEffectUniformPacking[] { }, new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
            new UnityEffectShaderStage("fd56c35980cc16d7b097c102c0745af1cbd2b6b661ff8ebf9d343bb042430038", 1, 32,
                new UnityEffectUniformPacking[]
                    { new UnityEffectUniformPacking(0, 0, 1, 0), new UnityEffectUniformPacking(0, 1, 1, 16) },
                new UnityEffectShaderInput[] { },
                new UnityEffectShaderTextureBinding[]
                    { new UnityEffectShaderTextureBinding(1, 0, 0, "_CelestePSTex0", "sampler_CelestePSTex0") }, 1u),
        };

        public static readonly UnityEffectShaderProgram[] Programs =
        {
            new UnityEffectShaderProgram("0ef836e79b7d72e3783257a9e2652ce74d6b6432427b06c0433c612e1411d430", Stages[47],
                Stages[79]),
            new UnityEffectShaderProgram("10227654adc2926f460983bed66c93f432b20de48ad4c83a36a1842db03568f6", Stages[47],
                Stages[55]),
            new UnityEffectShaderProgram("1639c4f91394ff6b2d7032473389dc65bd21c7757890c7008758c06b38983141", Stages[1],
                Stages[50]),
            new UnityEffectShaderProgram("17e9b97ed14c013a4f639827c4f7742b781fa1aa108506631170f15c531bfc6c", Stages[47],
                Stages[77]),
            new UnityEffectShaderProgram("19e52772c8ad3076e0dfb7edf33dace3d82c5568a960e5a58023694a863a53cc", Stages[0],
                Stages[40]),
            new UnityEffectShaderProgram("1d2151bda7b38a3562d4d79e55c2bfdf8ba7f90e635e6a2613e3337a839b3187", Stages[35],
                Stages[36]),
            new UnityEffectShaderProgram("1d3e97f534b9747912c3ba845b8657885fcb15ae338ad30bf016feb7ff752d0d", Stages[47],
                Stages[22]),
            new UnityEffectShaderProgram("1ebef9dec6f04a2af136a7cf1e640dc9d2f31dc26205c598353c59a38eae78d9", Stages[47],
                Stages[30]),
            new UnityEffectShaderProgram("2f5e8be182d4fe1d59275734ddc8d1f2c37f7046d338f1da3b4b5599399ce943", Stages[47],
                Stages[27]),
            new UnityEffectShaderProgram("30759144d8d51f5257670fae3bd64ad00d5a2517bb799f42e3c7d2847bccbce4", Stages[66],
                Stages[49]),
            new UnityEffectShaderProgram("365ca63dd7262b013fb6ed78d975f74d0f83706286b34b1d9da16ca85a1bef9b", Stages[47],
                Stages[83]),
            new UnityEffectShaderProgram("373666d3c8153cbb9c0ea8b82ca3871d414117da1d08c54a3047b8c815997da4", Stages[47],
                Stages[74]),
            new UnityEffectShaderProgram("3c53b6bc6e4f888afa8c853390ef1cdcf6099ee97c09f769aee6533e9e4b08c8", Stages[47],
                Stages[53]),
            new UnityEffectShaderProgram("3de82690ea7ecbd0e19fc24d0345776c0d23ee79148adc76fba9bc9b952a6ad7", Stages[71],
                Stages[69]),
            new UnityEffectShaderProgram("3f12ee28a33f2c8b37000870b7eaa9bea6fa3996ffa95df8ab775678c306b743", Stages[38],
                Stages[80]),
            new UnityEffectShaderProgram("3f1f3cb1e7e22fff5760a6907b64d124b300613eeca7fa1f183045d69ef6dae5", Stages[47],
                Stages[51]),
            new UnityEffectShaderProgram("42a881c3b96dbb855ad067518edadc02c4213c3f40a06f053d0894aa709c366c", Stages[54],
                Stages[43]),
            new UnityEffectShaderProgram("4e0fc5d6647b5b8f41be02563f658cbb8c607fc1942e5543ef4417d13c24bb87", Stages[12],
                Stages[7]),
            new UnityEffectShaderProgram("52af9042cad5f6a9dbd4b7560ad26ec0eee6387a5c8f467311da0245887dbd6d", Stages[1],
                Stages[45]),
            new UnityEffectShaderProgram("533bfa6f5b876a179c667b8147b6f46a05e7a8280d86a388ccf45975da8d4dec", Stages[38],
                Stages[63]),
            new UnityEffectShaderProgram("5a628b5ed8a9ebdd45abcabaca677c90f53ff9392587d3198f09f823beea9d21", Stages[47],
                Stages[64]),
            new UnityEffectShaderProgram("5d78f43f7f206a1db653fe1913ad3a7bb1fbeb7133f59e6f6abfb1feb0d6b5d9", Stages[25],
                Stages[57]),
            new UnityEffectShaderProgram("5dcb2e5cf25d1e2850f25b11ccc196eca16ba2e894725ffb1a8b41236ccf0d47", Stages[62],
                Stages[32]),
            new UnityEffectShaderProgram("60161b03e64b3cab9ada55e1153b2fd4ed249ff707036484edb556f94e766d96", Stages[60],
                Stages[8]),
            new UnityEffectShaderProgram("63815497e319765e4998e4985fdb7928ee9ebe20e7d9eea14901a075d5cfe150", Stages[47],
                Stages[16]),
            new UnityEffectShaderProgram("6474a89e3525e78d3a16fc96a4c1ec71e9096de959455a77062fa1cbcb939658", Stages[47],
                Stages[78]),
            new UnityEffectShaderProgram("658840ff14990892122296b21cbf5671f4c8b4c31c3a5badc16b82dfeb1ac4a6", Stages[73],
                Stages[34]),
            new UnityEffectShaderProgram("6b3d8b2b48d3e4a2a01caf68cda07885decbbffe9750d8ad8e6aa9db5fde6040", Stages[47],
                Stages[39]),
            new UnityEffectShaderProgram("701b5cd19ce00ac15db370eb9a0259563ac5a2b51e6bb25370dd03854a0aa189", Stages[72],
                Stages[11]),
            new UnityEffectShaderProgram("70ba68ed4670d897fa54430ad4f999fd0d156033aa9f384cd09eafa32beaac35", Stages[58],
                Stages[65]),
            new UnityEffectShaderProgram("712b38f43b6034fb54b8bc4b293855b2626006c3b75b562e436021eb2e50529e", Stages[31],
                Stages[18]),
            new UnityEffectShaderProgram("75058890946b6f75bb531c10efafd296a3cd34f10b9b3c5997f9e176ed955124", Stages[1],
                Stages[81]),
            new UnityEffectShaderProgram("7985f90d6f9f4869d87750d04db8ecfef98004e5d6686d93e781df9980c6aaad", Stages[37],
                Stages[48]),
            new UnityEffectShaderProgram("7d55139f1f7a3ea229159ab8f0f6170f8ed34a92f0238b5453660d3303a1deaf", Stages[47],
                Stages[2]),
            new UnityEffectShaderProgram("7dff804a68beea61a4d39ea7127ee1924ad3dab488e062ef622c1c40ba04b76d", Stages[15],
                Stages[7]),
            new UnityEffectShaderProgram("7e3739bd9297932ebf94aac70ef79af59ebdb08bab6b69235ffff43cfe9da8d1", Stages[75],
                Stages[29]),
            new UnityEffectShaderProgram("8406d2f47e2bd47083db1ec21b65ed9937b2c9ba5d4c09eb169014dadd46a8e4", Stages[24],
                Stages[69]),
            new UnityEffectShaderProgram("860ba8dd8182dc6e692de76ddf6c04c70345a87bc5a98a92299d22c843966c5f", Stages[0],
                Stages[65]),
            new UnityEffectShaderProgram("8aa16eb6e70f5aedc92ef873da0ff3f318ae61389fcded761184c6c946c790e0", Stages[47],
                Stages[70]),
            new UnityEffectShaderProgram("8b2700e2bb205571791fbce3075228aefea8c05ae658c77349b522bd76081337", Stages[42],
                Stages[28]),
            new UnityEffectShaderProgram("8d43c9c1bc0ce44e4fdfd70be4104aa6f086745d65190e5395def795f05a64ee", Stages[31],
                Stages[6]),
            new UnityEffectShaderProgram("8e04ba002c8b50256208749a8bb4dfc24e74d1ef00d133d7528a5b46349ed4ef", Stages[12],
                Stages[34]),
            new UnityEffectShaderProgram("9127b2ed9478684fd18cb760333d6ec995c1feb408d4048906dd8233a1123b3d", Stages[23],
                Stages[5]),
            new UnityEffectShaderProgram("9218bb5d6f35463621514f42f2c92d6cad13da01632bebca27404faf3c78fa9f", Stages[47],
                Stages[17]),
            new UnityEffectShaderProgram("950b89f86d445e546b0056c40d959dc0f9068458185e512e5dde2579630c657d", Stages[73],
                Stages[7]),
            new UnityEffectShaderProgram("97fbce4997882625e72991d36ef8e86e0ca4ae03878be8b1a9a1e29bc5ec3cc8", Stages[56],
                Stages[57]),
            new UnityEffectShaderProgram("99ddadaa5ae3d79349c322ca3cddc5c57cd0e6239c0fb9b37d1fa1df496f53b7", Stages[26],
                Stages[65]),
            new UnityEffectShaderProgram("a2376a8dcdb6bdaf3c0d3a756fc55273fbd34273b01277f656d7c13ecf84cbb3", Stages[47],
                Stages[46]),
            new UnityEffectShaderProgram("ae879a5ca1f35321c932464f56ecc1970e857bd19315086362a6141ab461b50b", Stages[47],
                Stages[4]),
            new UnityEffectShaderProgram("b18ee9a06c93dd8a7b2de83d790eecdc316391314928672f34cc8fed1dc65ad8", Stages[52],
                Stages[7]),
            new UnityEffectShaderProgram("b7c6f6cf1a33e96a966c6938155dfdf624d093b82d5b78794a5b57bc0f224f48", Stages[61],
                Stages[14]),
            new UnityEffectShaderProgram("bda9e1dfd226bec02c7a7e8c7402a6fa76a135aef74dec364c07c320f090436f", Stages[67],
                Stages[29]),
            new UnityEffectShaderProgram("c638ffe2e6f8216a23235041aa46fbec8457ef82b2b5a8143c9a6486063e91c1", Stages[47],
                Stages[59]),
            new UnityEffectShaderProgram("c7b745735030cfbcaf40e552153747be6cdbf94cd83c80ea82857a9d0958355a", Stages[44],
                Stages[43]),
            new UnityEffectShaderProgram("c8ce05ffc7ddbb9f299efec43d256f39a6b48357e8cdefd6d13f453977d38a2c", Stages[58],
                Stages[40]),
            new UnityEffectShaderProgram("ccfc9de9d01dc8141d33d308610cd03c1718cc289903ef30d5269362d657b728", Stages[52],
                Stages[34]),
            new UnityEffectShaderProgram("d2b0b42a13855467e6654e739684337ba813524c9db74f1a3bc36a30ca91711c", Stages[21],
                Stages[40]),
            new UnityEffectShaderProgram("d4007c5e3724dff1baca7d411f137059f3fb634fe8b585fcbe806ac6696f45e5", Stages[47],
                Stages[13]),
            new UnityEffectShaderProgram("d72dfe87aab1c98a608363710b27e08221a194e45a909e2850a8b1be931810d2", Stages[26],
                Stages[40]),
            new UnityEffectShaderProgram("de734d35fe18066957e1fe835945c2fa3f0b7ebae42226680ad7145b7fadb421", Stages[47],
                Stages[82]),
            new UnityEffectShaderProgram("e34fe7b0f757c908b0332c41c5fa8fffe942bee08ec153cb4c26cbb627065ce3", Stages[47],
                Stages[76]),
            new UnityEffectShaderProgram("e407cba52228a67f9119e979c245dc002881fb8b283c82fef1ab9b1e74ab2168", Stages[15],
                Stages[34]),
            new UnityEffectShaderProgram("e604e51fbb251c641fe463fa7ba2071e4d19e03f2512268dfd72dff158ee2c1f", Stages[47],
                Stages[33]),
            new UnityEffectShaderProgram("ea190742a4e1b525f846d590526a74e3f3f1840edb1ee64992052f674b7a42d1", Stages[47],
                Stages[10]),
            new UnityEffectShaderProgram("ef28e02d3c5dce5398ffc3ab4c65071888895e4966b9ee0776afeeb74b3b1466", Stages[1],
                Stages[20]),
            new UnityEffectShaderProgram("efe508c817af1f7c5bd87b701d10d66f26688b7126414b1355ee05fa1ab3184a", Stages[21],
                Stages[65]),
            new UnityEffectShaderProgram("fa2a2576b39a90939d94750ac799799708eb63da8b3373f15ebfc6883f27a2b5", Stages[1],
                Stages[19]),
            new UnityEffectShaderProgram("fe4faf2d50e5ce3d263756e3f719e0d78acbfe061da3f0968ed21ed759433618", Stages[3],
                Stages[28]),
            new UnityEffectShaderProgram("ff3b8ebbe74e30c396612d92b453b9409e26f6ee4e18d459b5d01b2ba63cdee9", Stages[9],
                Stages[36]),
        };
    }
}
