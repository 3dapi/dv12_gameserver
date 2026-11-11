// Config.h — "키 = 값" 형식의 설정 파일을 읽는다. '#' 뒤는 주석이다.
#pragma once
#include <charconv>
#include <fstream>
#include <map>
#include <sstream>
#include <stdexcept>
#include <string>

class Config
{
public:
    bool Load(const std::string& path)                  // 파일이 없으면 false
    {
        std::ifstream f(path);
        if (!f) return false;
        Parse(f);
        return true;
    }
    void Parse(std::istream& in)
    {
        std::string line;
        while (std::getline(in, line))
        {
            if (auto hash = line.find('#'); hash != std::string::npos) line.erase(hash);
            auto eq = line.find('=');
            if (eq == std::string::npos) continue;
            std::string k = Trim(line.substr(0, eq)), v = Trim(line.substr(eq + 1));
            if (!k.empty()) kv_[k] = v;
        }
    }
    std::string GetString(const std::string& key, const std::string& def) const
    {
        auto it = kv_.find(key);
        return it == kv_.end() ? def : it->second;
    }
    int GetInt(const std::string& key, int def) const   // 값이 있는데 숫자가 아니면 기본값으로 넘기지 않고 오류로 알린다
    {
        auto it = kv_.find(key);
        if (it == kv_.end()) return def;
        const std::string& s = it->second;
        int v = 0;
        auto r = std::from_chars(s.data(), s.data() + s.size(), v);
        if (r.ec != std::errc() || r.ptr != s.data() + s.size())
            throw std::runtime_error("config: '" + key + "' must be an integer, got '" + s + "'");
        return v;
    }
private:
    static std::string Trim(const std::string& s)
    {
        auto b = s.find_first_not_of(" \t\r"), e = s.find_last_not_of(" \t\r");
        return b == std::string::npos ? "" : s.substr(b, e - b + 1);
    }
    std::map<std::string, std::string> kv_;
};
