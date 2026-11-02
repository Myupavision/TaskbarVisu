namespace TaskbarVisu.Audio;

/// <summary>
/// Petit morceau synthétique bien stéréo pour les aperçus de la galerie : kick et basse au centre, accord
/// étalé de gauche à droite, mélodie qui se promène, charleston décorrélé. Écrit dans son propre tampon,
/// sur le thread de rendu, au rythme des images ; rien n'est joué sur la sortie audio.
/// </summary>
internal sealed class StereoDemo
{
    private const int Rate = 48000;
    private static readonly double[] Chord = { 220.0, 277.18, 329.63, 440.0 };
    private static readonly double[] ChordPan = { -0.5, -0.15, 0.2, 0.55 };

    private readonly Random _rng = new(3);
    private float[] _buffer = new float[2048];
    private long _n;
    private double _carry;

    public SampleRing Ring { get; } = new(1 << 15);

    public StereoDemo() => Ring.Reset(Rate);

    /// <summary>Ajoute au tampon le son de <paramref name="dt"/> secondes.</summary>
    public void Advance(double dt)
    {
        _carry += Math.Clamp(dt, 0, 0.1) * Rate;
        int frames = (int)_carry;
        _carry -= frames;
        if (frames == 0) return;
        if (_buffer.Length < frames * 2) _buffer = new float[frames * 2];
        for (int i = 0; i < frames; i++, _n++)
        {
            double t = _n / (double)Rate;
            double beat = t % 0.5;
            double kick = Math.Sin(2 * Math.PI * (50 + 110 * Math.Exp(-beat * 30)) * beat) * Math.Exp(-beat * 9) * 0.55;
            double bass = Math.Sin(2 * Math.PI * (t % 4 < 2 ? 55 : 41.2) * t) * 0.18 * Math.Exp(-beat * 2);

            double l = kick + bass, r = kick + bass;
            for (int k = 0; k < Chord.Length; k++)
            {
                // Chaque voix légèrement désaccordée entre gauche et droite : l'image respire.
                double f = Chord[k];
                double vl = Math.Sin(2 * Math.PI * f * t + k) + 0.3 * Math.Sin(4 * Math.PI * f * t);
                double vr = Math.Sin(2 * Math.PI * f * 1.0006 * t + k) + 0.3 * Math.Sin(4 * Math.PI * f * 1.0006 * t);
                double pan = ChordPan[k];
                l += vl * 0.06 * (1 - pan);
                r += vr * 0.06 * (1 + pan);
            }

            double fm = t % 2 < 1 ? 659.25 : 587.33;
            double lead = Math.Sin(2 * Math.PI * fm * t) * 0.08 * Math.Exp(-(t % 0.25) * 6);
            double sweep = 0.6 * Math.Sin(2 * Math.PI * 0.2 * t);
            l += lead * (1 - sweep) / 2;
            r += lead * (1 + sweep) / 2;

            double off = (t + 0.25) % 0.5;
            double hatEnv = Math.Exp(-off * 50) * 0.06;
            l += (_rng.NextDouble() * 2 - 1) * hatEnv;
            r += (_rng.NextDouble() * 2 - 1) * hatEnv;

            _buffer[i * 2] = (float)l;
            _buffer[i * 2 + 1] = (float)r;
        }
        Ring.Write(_buffer, frames);
    }
}
