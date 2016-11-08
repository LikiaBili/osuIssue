﻿//Copyright (c) 2007-2016 ppy Pty Ltd <contact@ppy.sh>.
//Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Transformations;
using OpenTK;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Textures;

namespace osu.Game.Beatmaps.Objects.Catch.Drawable
{
    class DrawableFruit : Sprite
    {
        private CatchBaseHit h;

        public DrawableFruit(CatchBaseHit h)
        {
            this.h = h;

            Origin = Anchor.Centre;
            Scale = new Vector2(0.1f);
            RelativePositionAxes = Axes.Y;
            Position = new Vector2(h.Position, -0.1f);
        }

        [Initializer]
        private void Load(TextureStore textures)
        {
            Texture = textures.Get(@"Menu/logo");

            Transforms.Add(new TransformPosition { StartTime = h.StartTime - 200, EndTime = h.StartTime, StartValue = new Vector2(h.Position, -0.1f), EndValue = new Vector2(h.Position, 0.9f) });
            Transforms.Add(new TransformAlpha { StartTime = h.StartTime + h.Duration + 200, EndTime = h.StartTime + h.Duration + 400, StartValue = 1, EndValue = 0 });
            Expire(true);
        }
    }
}
