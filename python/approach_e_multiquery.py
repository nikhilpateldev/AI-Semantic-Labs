
from rag import retrieve_context, build_prompt, call_llm
"""This improves recall dramatically — LLM expands user query into multiple semantic queries."""
def expand_query(q):
    prompt=f"""Rewrite into 3 semantic queries. Output Python list.
    Q:{q}"""
    r=call_llm(prompt)
    try: return eval(r)
    except: return [q]

def multi_query_retrieve(q,top_k=5):
    qs=expand_query(q)
    print("Expanded queries:", qs)
    merged={}
    for sub in qs:
        pts=retrieve_context(sub,top_k)
        for p in pts: merged[p.id]=p
    return list(merged.values())[:top_k]

def answer_multi(q):
    pts=multi_query_retrieve(q)
    prompt=build_prompt(q,pts)
    return call_llm(prompt), pts
