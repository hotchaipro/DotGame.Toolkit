// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.ComponentModel;

namespace DotGame.Toolkit.VisualStudio;

/// <summary>A setting's drop-down in the Properties window: one of a list of values, as the pipeline spells them.</summary>
public abstract class StandardValuesConverter : StringConverter
{
    private readonly StandardValuesCollection values;

    protected StandardValuesConverter(params string[] values)
    {
        this.values = new StandardValuesCollection(values);
    }

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) => this.values;
}

public sealed class CompressionConverter : StandardValuesConverter
{
    public CompressionConverter()
        : base("Lossless", "Lossy")
    {
    }
}

public sealed class QualityConverter : StandardValuesConverter
{
    public QualityConverter()
        : base("1", "2", "3", "4", "5", "6", "7", "8", "9", "10")
    {
    }
}

public sealed class BooleanValueConverter : StandardValuesConverter
{
    public BooleanValueConverter()
        : base("false", "true")
    {
    }
}

public sealed class LosslessFormatConverter : StandardValuesConverter
{
    public LosslessFormatConverter()
        : base("Default", "WAV", "FLAC")
    {
    }
}

public sealed class LossyFormatConverter : StandardValuesConverter
{
    public LossyFormatConverter()
        : base("Default", "Opus")
    {
    }
}
