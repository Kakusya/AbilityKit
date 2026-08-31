using System;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.BattleDemo;

namespace AbilityKit.Demo.Moba.Config.Core
{
    public interface IMobaConfigLoadProfile
    {
        string Name { get; }

        void Load(MobaConfigDatabase database);

        void Load(MobaConfigDatabase database, IMobaConfigLoadPipeline pipeline);
    }

    public sealed class ResourcesJsonMobaConfigLoadProfile : IMobaConfigLoadProfile
    {
        public static readonly ResourcesJsonMobaConfigLoadProfile Default = new ResourcesJsonMobaConfigLoadProfile();

        public ResourcesJsonMobaConfigLoadProfile(
            string resourcesDir = MobaConfigPaths.DefaultResourcesDir,
            bool strict = true)
        {
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            ResourcesDir = resourcesDir;
            Strict = strict;
        }

        public string Name => "ResourcesJson";

        public string ResourcesDir { get; }

        public bool Strict { get; }

        public void Load(MobaConfigDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));

            database.LoadFromResources(ResourcesDir, Strict);
        }

        public void Load(MobaConfigDatabase database, IMobaConfigLoadPipeline pipeline)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));

            var result = pipeline.ReloadFromResources(database, ResourcesDir, Strict);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload failed");
            }
        }
    }

    public sealed class SourceMobaConfigLoadProfile : IMobaConfigLoadProfile
    {
        private readonly IConfigSource _source;

        public SourceMobaConfigLoadProfile(IConfigSource source, string basePath = null, bool strict = true)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            BasePath = basePath;
            Strict = strict;
        }

        public string Name => "Source";

        public string BasePath { get; }

        public bool Strict { get; }

        public void Load(MobaConfigDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));

            var result = database.ReloadFromSource(_source, BasePath, Strict);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload failed");
            }
        }

        public void Load(MobaConfigDatabase database, IMobaConfigLoadPipeline pipeline)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));

            var result = pipeline.ReloadFromSource(database, _source, BasePath, Strict);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload failed");
            }
        }
    }

    public sealed class DtoProviderMobaConfigLoadProfile : IMobaConfigLoadProfile
    {
        private readonly IMobaConfigDtoProvider _provider;

        public DtoProviderMobaConfigLoadProfile(IMobaConfigDtoProvider provider, bool strict = true)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Strict = strict;
        }

        public string Name => "DtoProvider";

        public bool Strict { get; }

        public void Load(MobaConfigDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));

            var result = database.ReloadFromDtoProvider(_provider, Strict);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload failed");
            }
        }

        public void Load(MobaConfigDatabase database, IMobaConfigLoadPipeline pipeline)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));

            var result = pipeline.ReloadFromDtoProvider(database, _provider, Strict);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload failed");
            }
        }
    }
}
