
namespace BackDashMod;

class BackDash : ModBaseWithSettings<BackDash, Settings, object>, ICustomMenuMod
{
    [PreloadSharedAssets("Knight")]
    public static tk2dSpriteCollectionData newKnightColData = null!;
    [PreloadSharedAssets("Knight")]
    public static tk2dSpriteAnimation newKnightAnim = null!;
    public static tk2dSpriteCollectionData oldKnight = null!;
    public static List<tk2dSpriteAnimationClip> backdashClips = new();

    public bool ToggleButtonInsideMenu => false;

    private void OldKnightAnimLoad()
    {
        var tgo = new GameObject("Old Knight");
        UnityEngine.Object.DontDestroyOnLoad(tgo);
        oldKnight = tgo.AddComponent<tk2dSpriteCollectionData>();
        oldKnight.textures = new[] { ModRes.KNIGHT_ATLAS };

        JToken spriteInfo = JToken.Parse(ModRes.KNIGHT_SPRITES_TEXT);
        var sdef = spriteInfo["spriteDefinitions"]!;
        foreach (var v in sdef)
        {
            v["material"] = null;
        }
        oldKnight.spriteDefinitions = sdef.ToObject<tk2dSpriteDefinition[]>();

        var mat = UnityEngine.Object.Instantiate(newKnightColData.materials[0]);
        mat.mainTexture = oldKnight.textures[0];
        oldKnight.materials = new[] { mat };
        oldKnight.material = mat;
        foreach (var v in oldKnight.spriteDefinitions!)
        {
            v.material = mat;
        }

        oldKnight.invOrthoSize = 2;
        oldKnight.halfTargetHeight = 32;
        oldKnight.textureFilterMode = FilterMode.Bilinear;

        string[] requireClips = new[] { "Back Dash", "Backdash Land 1", "Backdash Land 2", "Back Dash 2", "Back Dash 3", "Back Dash Pause" };
        string[] origSpeed = new[] { "Backdash Land 1", "Backdash Land 2" };
        JToken animData = JToken.Parse(ModRes.KNIGHT_ANIM_TEXT);
        foreach (var v in animData["clips"]!)
        {
            var name = (string)v["name"]!;
            if (!requireClips.Contains(name)) continue;
            var clip = new tk2dSpriteAnimationClip();
            clip.name = name;
            clip.fps = (float)v["fps"]!;
            if (!origSpeed.Contains(name)) clip.fps *= 2;
            clip.loopStart = (int)v["loopStart"]!;
            clip.wrapMode = (tk2dSpriteAnimationClip.WrapMode)((int)v["wrapMode"]!);
            List<tk2dSpriteAnimationFrame> frames = new();

            foreach (var f in v["frames"]!)
            {
                var frame = new tk2dSpriteAnimationFrame();
                frame.spriteCollection = oldKnight;
                frame.spriteId = (int)f["spriteId"]!;
                frame.eventFloat = (float)f["eventFloat"]!;
                frame.eventInfo = (string)f["eventInfo"]!;
                frame.eventInt = (int)f["eventInt"]!;
                frame.triggerEvent = (bool)f["triggerEvent"]!;
                frames.Add(frame);
            }

            clip.frames = frames.ToArray();
            backdashClips.Add(clip);
        }
        newKnightAnim.clips = newKnightAnim.clips.Concat(backdashClips).ToArray();
    }
    public static void HeroBackDash()
    {
        var hc = HeroController.instance;
        hc.StopAnimationControl();
        hc.RelinquishControlNotVelocity();
        hc.ResetAttacks();
        hc.CancelBounce();
        hc.ResetLook();
        var ac = hc.private_audioCtrl();
        ac.StopSound(HeroSounds.FOOTSTEPS_RUN);
        ac.StopSound(HeroSounds.FOOTSTEPS_WALK);
        ac.PlaySound(HeroSounds.DASH);
        var ih = hc.private_inputHandler();
        if (hc.cState.wallSliding)
        {
            hc.FlipSprite();
        }
        else if (ih.inputActions.right.IsPressed)
        {
            hc.FaceRight();
        }
        else if (ih.inputActions.left.IsPressed)
        {
            hc.FaceLeft();
        }
        DoBackDash().StartCoroutine();
    }
    private static IEnumerator DoBackDash()
    {
        var hc = HeroController.instance;
        if (!hc.cState.onGround && !hc.inAcid)
		{
			hc.private_airDashed() = true;
		}
        var anim = hc.GetComponent<tk2dSpriteAnimator>();
        var move = DoBackDashMove().StartCoroutine();
        yield return null;
        yield return anim.PlayAnimWait("Back Dash");
        yield return anim.PlayAnimWait("Back Dash 2");
        yield return anim.PlayAnimWait("Back Dash 3");

        move.Stop();
        var rig = hc.private_rb2d();
        rig.velocity = Vector2.zero;
        yield return anim.PlayAnimWait("Backdash Land 1");
        yield return anim.PlayAnimWait("Backdash Land 2");
        hc.AffectedByGravity(true);
        hc.RegainControl();
        hc.StartAnimationControl();
    }
    private static IEnumerator DoBackDashMove()
    {
        var hc = HeroController.instance;
        var rig = hc.private_rb2d();
        var num = hc.DASH_SPEED;
        while (true)
        {
            hc.AffectedByGravity(false);
            hc.ResetHardLandingTimer();
            var result = new Vector2();
            if (hc.cState.facingRight)
            {
                if (hc.CheckForBump(CollisionSide.right))
                {
                    result = new Vector2(-num, (!hc.cState.onGround) ? 5f : 4f);
                }
                else
                {
                    result = new Vector2(-num, 0f);
                }
            }
            else if (hc.CheckForBump(CollisionSide.left))
            {
                result = new Vector2(num, (!hc.cState.onGround) ? 5f : 4f);
            }
            else
            {
                result = new Vector2(num, 0f);
            }
            rig.velocity = Modding.StaticModHooks.DashVelocityChange(result);
            yield return null;
        }
    }
    public override void Initialize()
    {
        OldKnightAnimLoad();
        ModHooks.HeroUpdateHook += () => {
            var hc = HeroController.instance;
            if(hc.CanDash() && globalSettings.keySettings.backDash.IsPressed) {
                HeroBackDash();
            } 
        };
    }

    public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates)
    {
        return new SettingsMenu(modListMenu);
    }
}
