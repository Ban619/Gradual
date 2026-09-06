/*
 * FronTime PMS - Internal Type Checker (C++ Performance Optimizer)
 * Ultra-fast pre-scanning to optimize Python analysis
 * Works in tandem with Tfile.py for maximum performance
 */

#include <iostream>
#include <fstream>
#include <string>
#include <vector>
#include <filesystem>
#include <unordered_set>
#include <unordered_map>
#include <algorithm>
#include <chrono>
#include <sstream>

namespace fs = std::filesystem;

/**
 * Performance-optimized file scanner
 * Provides quick pre-analysis before Python deep scan
 */
class FastScanner {
private:
    // Cached file type sets loaded from type.txt
    std::unordered_set<std::string> acceptedExtensions;
    std::unordered_set<std::string> rejectedExtensions;
    std::unordered_set<std::string> projectMarkers;
    
    // Performance counters
    struct ScanStats {
        int totalFiles = 0;
        int acceptedFiles = 0;
        int rejectedFiles = 0;
        int unknownFiles = 0;
        int markerFiles = 0;
        double scanTimeMs = 0.0;
        bool hasCode = false;
        bool hasOnlyMedia = false;
        bool hasUnknown = false;
    };

public:
    FastScanner() {
        // Load type database from type.txt
        loadTypeDatabase();
    }
    
    /**
     * Ultra-fast pre-scan (surface level only)
     * Returns true if Python should do full analysis
     * Returns false if can immediately reject
     */
    bool shouldAnalyze(const std::string& directoryPath) {
        auto stats = quickScan(directoryPath);
        
        // Immediate rejection criteria
        if (stats.totalFiles == 0) {
            return false; // Empty directory
        }
        
        if (stats.unknownFiles > 0) {
            return true; // Has unknown files - let Python decide
        }
        
        if (stats.acceptedFiles == 0 && stats.rejectedFiles > 0) {
            return false; // Only rejected files, no code
        }
        
        if (stats.hasMarkerFiles) {
            return true; // Has project markers - definitely analyze
        }
        
        if (stats.hasCode) {
            return true; // Has code files - worth analyzing
        }
        
        if (stats.hasOnlyMedia) {
            return false; // Only media files - reject immediately
        }
        
        // Default: let Python decide
        return true;
    }
    
    /**
     * Lightning-fast surface scan
     * Only counts file types, no deep logic
     */
    ScanStats quickScan(const std::string& directoryPath) {
        ScanStats stats;
        auto startTime = std::chrono::high_resolution_clock::now();
        
        try {
            if (!fs::exists(directoryPath) || !fs::is_directory(directoryPath)) {
                return stats;
            }
            
            // Surface level scan only (performance optimization)
            for (const auto& entry : fs::directory_iterator(directoryPath)) {
                if (entry.is_regular_file()) {
                    processFile(entry.path(), stats);
                }
            }
            
            // Analyze pattern
            stats.hasCode = stats.acceptedFiles > 0;
            stats.hasOnlyMedia = (stats.rejectedFiles > 0 && stats.acceptedFiles == 0 && stats.unknownFiles == 0);
            stats.hasUnknown = stats.unknownFiles > 0;
            
            auto endTime = std::chrono::high_resolution_clock::now();
            auto duration = std::chrono::duration_cast<std::chrono::microseconds>(endTime - startTime);
            stats.scanTimeMs = duration.count() / 1000.0;
            
        } catch (const std::exception& e) {
            std::cerr << "C++ Scan Error: " << e.what() << std::endl;
        }
        
        return stats;
    }
    
    /**
     * Get scan statistics as JSON for C# interop
     */
    std::string getStatsJson(const ScanStats& stats) {
        std::ostringstream json;
        json << "{\n";
        json << "  \"totalFiles\": " << stats.totalFiles << ",\n";
        json << "  \"acceptedFiles\": " << stats.acceptedFiles << ",\n";
        json << "  \"rejectedFiles\": " << stats.rejectedFiles << ",\n";
        json << "  \"unknownFiles\": " << stats.unknownFiles << ",\n";
        json << "  \"markerFiles\": " << stats.markerFiles << ",\n";
        json << "  \"hasCode\": " << (stats.hasCode ? "true" : "false") << ",\n";
        json << "  \"hasOnlyMedia\": " << (stats.hasOnlyMedia ? "true" : "false") << ",\n";
        json << "  \"hasUnknown\": " << (stats.hasUnknown ? "true" : "false") << ",\n";
        json << "  \"scanTimeMs\": " << stats.scanTimeMs << "\n";
        json << "}";
        return json.str();
    }

private:
    void loadTypeDatabase() {
        // Try to load from type.txt
        std::string typeDbPath = "T/type.txt";
        std::ifstream file(typeDbPath);
        
        if (!file.is_open()) {
            // Fallback to hardcoded essentials for speed
            loadDefaultTypes();
            return;
        }
        
        std::string line;
        std::string currentSection;
        
        while (std::getline(file, line)) {
            // Trim whitespace
            line.erase(0, line.find_first_not_of(" \t\r\n"));
            line.erase(line.find_last_not_of(" \t\r\n") + 1);
            
            // Skip empty lines and comments
            if (line.empty() || line[0] == '#') {
                continue;
            }
            
            // Section headers
            if (line[0] == '[' && line[line.length()-1] == ']') {
                currentSection = line.substr(1, line.length()-2);
                continue;
            }
            
            // Parse entries
            if (line.find('|') != std::string::npos) {
                std::istringstream iss(line);
                std::string category, extension, description;
                
                std::getline(iss, category, '|');
                std::getline(iss, extension, '|');
                
                // Convert extension to lowercase
                std::transform(extension.begin(), extension.end(), extension.begin(), ::tolower);
                
                // Add to appropriate set
                if (currentSection.find("ACCEPTED") != std::string::npos) {
                    if (category == "MARKER") {
                        projectMarkers.insert(extension);
                    } else {
                        acceptedExtensions.insert(extension);
                    }
                } else if (currentSection.find("REJECTED") != std::string::npos) {
                    rejectedExtensions.insert(extension);
                }
            }
        }
        
        file.close();
        
        std::cout << "C++ Loaded: " << acceptedExtensions.size() << " accepted, "
                  << rejectedExtensions.size() << " rejected types" << std::endl;
    }
    
    void loadDefaultTypes() {
        // Minimal essential types for performance
        acceptedExtensions = {
            ".cs", ".py", ".cpp", ".c", ".h", ".hpp", ".java", ".js", ".ts",
            ".jsx", ".tsx", ".go", ".rs", ".rb", ".php", ".json", ".xml"
        };
        
        rejectedExtensions = {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".mp4", ".avi", ".mov",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".mp3", ".wav"
        };
        
        projectMarkers = {
            "package.json", "requirements.txt", "readme.md", ".gitignore",
            "cargo.toml", "go.mod", "pom.xml", "makefile"
        };
    }
    
    void processFile(const fs::path& filePath, ScanStats& stats) {
        stats.totalFiles++;
        
        std::string filename = filePath.filename().string();
        std::string extension = filePath.extension().string();
        
        // Convert to lowercase for comparison
        std::transform(filename.begin(), filename.end(), filename.begin(), ::tolower);
        std::transform(extension.begin(), extension.end(), extension.begin(), ::tolower);
        
        // Check for project markers (highest priority)
        if (projectMarkers.count(filename)) {
            stats.markerFiles++;
            stats.acceptedFiles++;
            return;
        }
        
        // Check extension against database
        if (acceptedExtensions.count(extension)) {
            stats.acceptedFiles++;
        } else if (rejectedExtensions.count(extension)) {
            stats.rejectedFiles++;
        } else {
            stats.unknownFiles++; // Unknown/anonymous file type
        }
    }
};

// C-style interface for C# P/Invoke
extern "C" {
    
    /**
     * Fast pre-check: Should Python analyzer run full analysis?
     * Returns: 1 for yes (analyze), 0 for no (reject immediately)
     */
    __declspec(dllexport) int ShouldAnalyzeDirectory(const char* path) {
        try {
            FastScanner scanner;
            return scanner.shouldAnalyze(std::string(path)) ? 1 : 0;
        } catch (...) {
            return 1; // On error, let Python decide
        }
    }
    
    /**
     * Get quick scan statistics as JSON
     * Returns: JSON string (caller must free with FreeString)
     */
    __declspec(dllexport) const char* GetQuickScanStats(const char* path) {
        try {
            FastScanner scanner;
            auto stats = scanner.quickScan(std::string(path));
            std::string json = scanner.getStatsJson(stats);
            
            // Allocate string for return
            char* output = new char[json.length() + 1];
            strcpy_s(output, json.length() + 1, json.c_str());
            return output;
        } catch (...) {
            return nullptr;
        }
    }
    
    /**
     * Free memory allocated by GetQuickScanStats
     */
    __declspec(dllexport) void FreeString(const char* str) {
        delete[] str;
    }
    
    /**
     * Optimize signal/identifier speed
     * Pre-processes path for optimal Python handoff
     */
    __declspec(dllexport) int OptimizeScanSignal(const char* path) {
        try {
            FastScanner scanner;
            auto stats = scanner.quickScan(std::string(path));
            
            // Return optimization hint:
            // 0 = Reject immediately (no Python needed)
            // 1 = Quick Python scan sufficient
            // 2 = Deep Python scan recommended
            
            if (stats.hasOnlyMedia || stats.totalFiles == 0) {
                return 0; // Reject without Python
            }
            
            if (stats.hasCode || stats.markerFiles > 0) {
                return 1; // Quick Python scan
            }
            
            if (stats.hasUnknown) {
                return 2; // Deep Python scan needed
            }
            
            return 1; // Default: quick scan
        } catch (...) {
            return 2; // Error: do deep scan
        }
    }
}

// Standalone executable for testing
int main(int argc, char* argv[]) {
    if (argc < 2) {
        std::cout << "Usage: internalT <directory_path>" << std::endl;
        std::cout << "Performance Optimizer for Tfile.py" << std::endl;
        return 1;
    }
    
    std::string directoryPath = argv[1];
    
    FastScanner scanner;
    
    std::cout << "=== C++ Performance Optimizer ===" << std::endl;
    std::cout << "Directory: " << directoryPath << std::endl;
    std::cout << std::endl;
    
    auto stats = scanner.quickScan(directoryPath);
    
    std::cout << "Quick Scan Results:" << std::endl;
    std::cout << "  Total Files: " << stats.totalFiles << std::endl;
    std::cout << "  Accepted Files: " << stats.acceptedFiles << std::endl;
    std::cout << "  Rejected Files: " << stats.rejectedFiles << std::endl;
    std::cout << "  Unknown Files: " << stats.unknownFiles << std::endl;
    std::cout << "  Marker Files: " << stats.markerFiles << std::endl;
    std::cout << "  Has Code: " << (stats.hasCode ? "Yes" : "No") << std::endl;
    std::cout << "  Only Media: " << (stats.hasOnlyMedia ? "Yes" : "No") << std::endl;
    std::cout << "  Scan Time: " << stats.scanTimeMs << " ms" << std::endl;
    std::cout << std::endl;
    
    bool shouldAnalyze = scanner.shouldAnalyze(directoryPath);
    std::cout << "Decision: " << (shouldAnalyze ? "RUN PYTHON ANALYSIS" : "REJECT IMMEDIATELY") << std::endl;
    
    if (shouldAnalyze) {
        std::cout << std::endl << "Next: python Tfile.py \"" << directoryPath << "\"" << std::endl;
    }
    
    return shouldAnalyze ? 0 : 1;
}
