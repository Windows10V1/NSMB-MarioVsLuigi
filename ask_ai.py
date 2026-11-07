import sys
import os
import torch
from airllm import AutoModel

MODEL_ID = "Qwen/Qwen2.5-Coder-7B-Instruct"

print(f"--- Initializing AirLLM with {MODEL_ID} ---")
model = AutoModel.from_pretrained(
    MODEL_ID,
    compression="4bit"
)

def run_prompt(user_prompt):
    input_tokens = model.tokenizer(
        [user_prompt], 
        return_tensors="pt", 
        return_attention_mask=False, 
        truncation=True, 
        max_length=2048, 
        padding=False
    )
    
    input_ids = input_tokens['input_ids']
        
    generation_output = model.generate(
        input_ids, 
        max_new_tokens=512, 
        use_cache=True, 
        return_dict_in_generate=True
    )
    
    return model.tokenizer.decode(generation_output.sequences[0], skip_special_tokens=True)

if __name__ == "__main__":
    if len(sys.argv) > 1:
        prompt_arg = sys.argv[1]
        
        if os.path.exists(prompt_arg):
            with open(prompt_arg, 'r', encoding='utf-8') as f:
                code_content = f.read()
            prompt = (
                "You are an expert deterministic C# / Unity developer.\n"
                "Review the following code file from my repository and suggest improvements or fixes:\n\n"
                f"{code_content}"
            )
        else:
            prompt = prompt_arg
    else:
        prompt = "Write a deterministic C# helper function to compute a 2D trajectory."

    print("\n[Asking AirLLM Assistant...]\n")
    response = run_prompt(prompt)
    print("--- AI RESPONSE ---")
    print(response)