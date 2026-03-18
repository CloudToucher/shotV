using Godot;

namespace ShotV.Core;

public static class Palette
{
    public static Color FromHex(uint hex)
    {
        float r = ((hex >> 16) & 0xFF) / 255f;
        float g = ((hex >> 8) & 0xFF) / 255f;
        float b = (hex & 0xFF) / 255f;
        return new Color(r, g, b, 1f);
    }

    public static readonly Color BgOuter = FromHex(0xf0f7fa);
    public static readonly Color BgInner = FromHex(0xffffff);
    public static readonly Color WorldFloor = FromHex(0xf4f9fc);
    public static readonly Color WorldFloorDeep = FromHex(0xe8f1f5);
    public static readonly Color WorldAccent = FromHex(0xe0ecf3);
    public static readonly Color WorldDust = FromHex(0xe0ecf3);
    public static readonly Color WorldLine = FromHex(0xdce8f0);
    public static readonly Color WorldLineStrong = FromHex(0xc8dce6);

    public static readonly Color ObstacleFill = FromHex(0xebf3f8);
    public static readonly Color ObstacleInner = FromHex(0xf4f9fc);
    public static readonly Color ObstacleEdge = FromHex(0x9fb8c6);
    public static readonly Color ObstacleShadow = FromHex(0xbacdd8);
    public static readonly Color ObstacleWall = FromHex(0xebf3f8);
    public static readonly Color ObstacleCover = FromHex(0xe2edf4);
    public static readonly Color ObstacleStation = FromHex(0xe6f0ea);
    public static readonly Color ObstacleLocker = FromHex(0xf0ebd9);

    public static readonly Color MinimapBg = FromHex(0xffffff);
    public static readonly Color MinimapBorder = FromHex(0x4db9e6);
    public static readonly Color MinimapObstacle = FromHex(0xbacdd8);
    public static readonly Color MinimapPlayer = FromHex(0x4db9e6);
    public static readonly Color MinimapEnemy = FromHex(0xff6b6b);
    public static readonly Color MinimapMarker = FromHex(0xffb033);

    public static readonly Color ArenaFill = FromHex(0xf4f9fc);
    public static readonly Color ArenaCore = FromHex(0x4db9e6);
    public static readonly Color Grid = FromHex(0xdce8f0);
    public static readonly Color Frame = FromHex(0x4db9e6);
    public static readonly Color FrameSoft = FromHex(0x9bd4eb);
    public static readonly Color PanelLine = FromHex(0x4db9e6);
    public static readonly Color PanelWarm = FromHex(0xffb033);

    public static readonly Color PlayerBody = FromHex(0xffffff);
    public static readonly Color PlayerEdge = FromHex(0x63cff5);
    public static readonly Color PlayerCore = FromHex(0x1e9fe0);

    public static readonly Color Accent = FromHex(0xff9d4d);
    public static readonly Color AccentSoft = FromHex(0xffcc8f);
    public static readonly Color Reticle = FromHex(0x2a5870);
    public static readonly Color Shot = FromHex(0xffb066);
    public static readonly Color Dash = FromHex(0x4ebeea);
    public static readonly Color Danger = FromHex(0xe46d61);
    public static readonly Color Warning = FromHex(0xd9a858);

    public static readonly Color EnemyEdge = FromHex(0x143245);
    public static readonly Color EnemyMelee = FromHex(0xff8d76);
    public static readonly Color EnemyMeleeGlow = FromHex(0xffb89a);
    public static readonly Color EnemyRanged = FromHex(0xffc168);
    public static readonly Color EnemyRangedGlow = FromHex(0xffddb2);
    public static readonly Color EnemyCharger = FromHex(0xff6686);
    public static readonly Color EnemyChargerGlow = FromHex(0xff9eb0);
    public static readonly Color EnemyStalker = FromHex(0xff7a94);
    public static readonly Color EnemyStalkerGlow = FromHex(0xffb0c2);
    public static readonly Color EnemySuppressor = FromHex(0xff9f56);
    public static readonly Color EnemySuppressorGlow = FromHex(0xffc98b);
    public static readonly Color EnemyBoss = FromHex(0xff5d4c);
    public static readonly Color EnemyBossGlow = FromHex(0xff9d74);
    public static readonly Color EnemyProjectile = FromHex(0xff875c);

    public static readonly Color UiText = FromHex(0x143245);
    public static readonly Color UiMuted = FromHex(0x6a8c9e);
    public static readonly Color UiPanel = FromHex(0xffffff);
    public static readonly Color UiActive = FromHex(0xe8f4fa);
}
