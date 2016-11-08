﻿//Copyright (c) 2007-2016 ppy Pty Ltd <contact@ppy.sh>.
//Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using OpenTK;
using OpenTK.Graphics;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Textures;

namespace osu.Game.GameModes.Play.Taiko
{
    public class TaikoPlayfield : Playfield
    {
        public TaikoPlayfield()
        {
            RelativeSizeAxes = Axes.X;
            Size = new Vector2(1, 100);
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [Initializer]
        private void Load(TextureStore textures)
        {
            Add(new Box { RelativeSizeAxes = Axes.Both, Alpha = 0.5f });

            Add(new Sprite
            {
                Texture = textures.Get(@"Menu/logo"),
                Origin = Anchor.Centre,
                Scale = new Vector2(0.2f),
                RelativePositionAxes = Axes.Both,
                Position = new Vector2(0.1f, 0.5f),
                Colour = Color4.Gray
            });
        }
    }
}