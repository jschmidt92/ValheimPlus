namespace ValheimPlus.Configurations.Sections
{
    public class AutoStackConfiguration : ServerSyncConfig<AutoStackConfiguration>
    {
        public float autoStackAllRange { get; internal set; } = 10;
        public bool autoStackAllIgnorePrivateAreaCheck { get; internal set; } = false;
        public bool autoStackAllIgnoreEquipment { get; internal set; } = false;
        public bool ignoreAmmo { get; internal set; } = false;
        public bool ignoreFood { get; internal set; } = false;
        public bool ignoreMead { get; internal set; } = false;
    }
}
