namespace AIChatBot.Generators.Text.Oobabooga;

internal struct Parameters
{
    public Parameters(string prompt = "")
    {
        this.prompt = prompt;
    }

    public readonly string prompt;

    // public readonly ? stop_at_newline = ,
    // public readonly ? chat_prompt_size_slider = ,
    // public readonly ? chat_generation_attempts = ,
    public readonly int max_new_tokens = 200;
    public readonly bool do_sample = false;
    public readonly double temperature = 0.99;
    public readonly double top_p = 0.9;
    public readonly int typical_p = 1;
    public readonly double repetition_penalty = 1.1;
    public readonly int encoder_repetition_penalty = 1;
    public readonly int top_k = 40;
    public readonly int num_beams = 1;
    public readonly int penalty_alpha = 0;
    public readonly int min_length = 0;
    public readonly int length_penalty = 1;
    public readonly int no_repeat_ngram_size = 1;
    public readonly bool early_stopping = true;
    public readonly string[] custom_stopping_strings = new[] { @"\n[", "\n[", "]:", "##", "###", "<noinput>", @"\end" };
    public readonly int seed = -1;
    public readonly bool add_bos_token = true;
}