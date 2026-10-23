import { create } from "zustand";

/** The top bar's search text (the /search page reads ?q=; the box keeps the two in sync). */
type SearchStore = {
  query: string;
  setQuery: (query: string) => void;
};

export const useSearchStore = create<SearchStore>()((set) => ({
  query: "",
  setQuery: (query) => set({ query }),
}));
