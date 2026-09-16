"""
FronTime PMS - File Type Identifier & Project Validator
Reads type.txt database and validates project folders.
REJECTS folders with only media/documents or unknown file types.
"""

import os
import sys
import json
from pathlib import Path
from typing import Dict, List, Tuple, Set

class ProjectIdentifier:
    """
    Identifies and validates project folders using type.txt database.
    Rejects folders that only contain non-programming files or unknown types.
    """
    
    def __init__(self, database_path: str = None):
        """Initialize with type.txt database."""
        # File type databases
        self.accepted_code = set()
        self.accepted_config = set()
        self.accepted_markers = set()
        self.accepted_web = set()
        self.accepted_data = set()
        self.rejected_images = set()
        self.rejected_videos = set()
        self.rejected_audio = set()
        self.rejected_documents = set()
        self.rejected_archives = set()
        self.ignored_types = set()
        
        # Settings
        self.unknown_policy = 'REJECT_ALL'
        self.min_code_files = 3
        self.min_programming_percentage = 30.0
        self.max_media_percentage = 80.0
        self.max_scan_depth = 1
        
        # Load database
        if database_path is None:
            # Default path relative to this script
            script_dir = Path(__file__).parent
            database_path = script_dir / 'T' / 'type.txt'
        
        self.load_database(database_path)
    
    def load_database(self, database_path: Path):
        """Load file type database from type.txt."""
        try:
            if not Path(database_path).exists():
                print(f"Warning: Database file not found: {database_path}", file=sys.stderr)
                print("Using default file types...", file=sys.stderr)
                self._load_defaults()
                return
            
            with open(database_path, 'r', encoding='utf-8') as f:
                current_section = None
                
                for line in f:
                    line = line.strip()
                    
                    # Skip empty lines and comments
                    if not line or line.startswith('#'):
                        continue
                    
                    # Section headers
                    if line.startswith('[') and line.endswith(']'):
                        current_section = line[1:-1]
                        continue
                    
                    # Parse entries
                    if '|' in line:
                        parts = line.split('|')
                        if len(parts) >= 2:
                            category = parts[0].strip()
                            extension = parts[1].strip().lower()
                            
                            # Add to appropriate set
                            if current_section == 'ACCEPTED_CODE' and category == 'CODE':
                                self.accepted_code.add(extension)
                            elif current_section == 'ACCEPTED_CONFIG' and category == 'CONFIG':
                                self.accepted_config.add(extension)
                            elif current_section == 'ACCEPTED_MARKERS' and category == 'MARKER':
                                self.accepted_markers.add(extension.lower())
                            elif current_section == 'ACCEPTED_WEB' and category == 'WEB':
                                self.accepted_web.add(extension)
                            elif current_section == 'ACCEPTED_DATA' and category == 'DATA':
                                self.accepted_data.add(extension)
                            elif current_section == 'REJECTED_IMAGES' and category == 'IMAGE':
                                self.rejected_images.add(extension)
                            elif current_section == 'REJECTED_VIDEOS' and category == 'VIDEO':
                                self.rejected_videos.add(extension)
                            elif current_section == 'REJECTED_AUDIO' and category == 'AUDIO':
                                self.rejected_audio.add(extension)
                            elif current_section == 'REJECTED_DOCUMENTS' and category == 'DOCUMENT':
                                self.rejected_documents.add(extension)
                            elif current_section == 'REJECTED_ARCHIVES' and category == 'ARCHIVE':
                                self.rejected_archives.add(extension)
                            elif current_section == 'IGNORED' and category == 'IGNORE':
                                self.ignored_types.add(extension)
                    
                    # Parse settings
                    if '=' in line and current_section == 'THRESHOLDS':
                        key, value = line.split('=', 1)
                        key = key.strip()
                        value = value.strip()
                        
                        if key == 'MIN_CODE_FILES':
                            self.min_code_files = int(value)
                        elif key == 'MIN_PROGRAMMING_PERCENTAGE':
                            self.min_programming_percentage = float(value)
                        elif key == 'MAX_MEDIA_PERCENTAGE':
                            self.max_media_percentage = float(value)
                    
                    if '=' in line and current_section == 'ANONYMOUS_POLICY':
                        key, value = line.split('=', 1)
                        if key.strip() == 'UNKNOWN_FILE_POLICY':
                            self.unknown_policy = value.strip()
                    
                    if '=' in line and current_section == 'SCAN_SETTINGS':
                        key, value = line.split('=', 1)
                        if key.strip() == 'MAX_SCAN_DEPTH':
                            self.max_scan_depth = int(value.strip())
            
            print(f"Loaded database: {len(self.accepted_code)} code types, "
                  f"{len(self.rejected_images)} image types", file=sys.stderr)
                  
        except Exception as e:
            print(f"Error loading database: {e}", file=sys.stderr)
            self._load_defaults()
    
    def _load_defaults(self):
        """Load minimal default file types if database unavailable."""
        self.accepted_code = {'.cs', '.py', '.cpp', '.c', '.h', '.java', '.js', '.ts'}
        self.accepted_config = {'.json', '.xml', '.yaml', '.csproj', '.sln'}
        self.accepted_markers = {'package.json', 'requirements.txt', 'readme.md'}
        self.rejected_images = {'.jpg', '.jpeg', '.png', '.gif', '.bmp'}
        self.rejected_videos = {'.mp4', '.avi', '.mov', '.wmv'}
        self.rejected_documents = {'.pdf', '.doc', '.docx', '.xls', '.xlsx'}
    
    def scan_directory(self, directory_path: str, max_depth: int = None) -> Dict:
        """
        Scans a directory using type.txt database as reference.
        REJECTS if:
        - Only media/document files exist
        - Unknown/anonymous file types detected
        - No programming files found
        
        Args:
            directory_path: Path to scan
            max_depth: Override default scan depth
        
        Returns:
            Dictionary with validation results
        """
        if max_depth is None:
            max_depth = self.max_scan_depth
        
        try:
            path = Path(directory_path)
            
            if not path.exists():
                return self._create_error_result("Directory does not exist")
            
            if not path.is_dir():
                return self._create_error_result("Path is not a directory")
            
            # Initialize counters
            file_counts = {
                'accepted_code': 0,
                'accepted_config': 0,
                'accepted_web': 0,
                'accepted_data': 0,
                'rejected_media': 0,
                'rejected_documents': 0,
                'unknown_files': 0,
                'ignored_files': 0,
                'total_files': 0
            }
            
            found_markers = []
            unknown_extensions = set()
            sample_files = {
                'code': [],
                'rejected': [],
                'unknown': []
            }
            
            # Scan directory
            self._scan_recursive(path, file_counts, found_markers, 
                                unknown_extensions, sample_files,
                                current_depth=0, max_depth=max_depth)
            
            # Analyze results
            return self._analyze_with_database(directory_path, file_counts, 
                                               found_markers, unknown_extensions,
                                               sample_files)
            
        except PermissionError:
            return self._create_error_result("Permission denied")
        except Exception as e:
            return self._create_error_result(f"Scan error: {str(e)}")
    
    def _scan_recursive(self, path: Path, counts: Dict, markers: List,
                        unknown_exts: Set, samples: Dict,
                        current_depth: int, max_depth: int):
        """Recursively scans directory using database."""
        
        if current_depth > max_depth:
            return
        
        try:
            for item in path.iterdir():
                # Skip hidden files except known markers
                if item.name.startswith('.'):
                    if item.name.lower() not in self.accepted_markers:
                        continue
                
                if item.is_file():
                    self._process_file_with_database(item, counts, markers,
                                                     unknown_exts, samples)
                elif item.is_dir() and current_depth < max_depth:
                    # Scan deeper
                    self._scan_recursive(item, counts, markers, unknown_exts,
                                        samples, current_depth + 1, max_depth)
        except PermissionError:
            pass
    
    def _process_file_with_database(self, file_path: Path, counts: Dict,
                                     markers: List, unknown_exts: Set,
                                     samples: Dict):
        """Process file using type.txt database."""
        
        counts['total_files'] += 1
        ext = file_path.suffix.lower()
        filename = file_path.name.lower()
        
        # Skip special files (PowerShell/CMD artifacts)
        if filename in ['$null', 'null', 'nul']:
            counts['ignored_files'] += 1
            return
        
        # Check for project markers first (highest priority)
        if filename in self.accepted_markers:
            markers.append(filename)
            counts['accepted_config'] += 1
            return
        
        # Check against database categories
        if ext in self.accepted_code:
            counts['accepted_code'] += 1
            if len(samples['code']) < 5:
                samples['code'].append(file_path.name)
        
        elif ext in self.accepted_config:
            counts['accepted_config'] += 1
        
        elif ext in self.accepted_web:
            counts['accepted_web'] += 1
            if len(samples['code']) < 5:
                samples['code'].append(file_path.name)
        
        elif ext in self.accepted_data:
            counts['accepted_data'] += 1
        
        elif ext in self.rejected_images or ext in self.rejected_videos or \
             ext in self.rejected_audio or ext in self.rejected_documents or \
             ext in self.rejected_archives:
            counts['rejected_media'] += 1
            if len(samples['rejected']) < 5:
                samples['rejected'].append(file_path.name)
        
        elif ext in self.ignored_types:
            counts['ignored_files'] += 1
        
        else:
            # UNKNOWN/ANONYMOUS FILE TYPE
            counts['unknown_files'] += 1
            unknown_exts.add(ext if ext else f"<no-extension:{filename}>")
            if len(samples['unknown']) < 5:
                samples['unknown'].append(file_path.name)
    
    def _analyze_with_database(self, directory: str, counts: Dict,
                                markers: List, unknown_exts: Set,
                                samples: Dict) -> Dict:
        """Analyze results using database rules."""
        
        total_files = counts['total_files']
        accepted_files = (counts['accepted_code'] + counts['accepted_config'] +
                         counts['accepted_web'] + counts['accepted_data'])
        rejected_files = counts['rejected_media']
        unknown_files = counts['unknown_files']
        
        # Calculate percentages
        if total_files == 0:
            return self._create_rejection("Directory is empty", counts, markers, samples)
        
        programming_percentage = (accepted_files / total_files * 100)
        rejected_percentage = (rejected_files / total_files * 100)
        unknown_percentage = (unknown_files / total_files * 100)
        
        # CRITICAL REJECTION RULES
        
        # Rule 1: Unknown/Anonymous files detected (based on policy)
        if self.unknown_policy == 'REJECT_ALL' and unknown_files > 0:
            return self._create_rejection(
                f"REJECTED: {unknown_files} unknown/anonymous file type(s) detected: {', '.join(list(unknown_exts)[:5])}",
                counts, markers, samples, confidence=0
            )
        
        # Rule 2: ONLY rejected files (no code at all)
        if accepted_files == 0 and rejected_files > 0:
            return self._create_rejection(
                f"REJECTED: Directory contains ONLY media/document files ({rejected_files} files, 0 code files)",
                counts, markers, samples, confidence=0
            )
        
        # Rule 3: High percentage of rejected files with minimal code
        if rejected_percentage > self.max_media_percentage and counts['accepted_code'] == 0:
            return self._create_rejection(
                f"REJECTED: {rejected_percentage:.1f}% media/documents with no source code files",
                counts, markers, samples, confidence=0
            )
        
        # Rule 4: Too few files total
        if total_files < 2 and not markers:
            return self._create_rejection(
                "REJECTED: Directory has too few files and no project markers",
                counts, markers, samples, confidence=0
            )
        
        # ACCEPTANCE CRITERIA
        
        confidence = 0
        classification = "Unknown"
        
        # High confidence: Has project markers
        if markers:
            confidence = 90 + min(len(markers) * 2, 10)
            classification = self._classify_by_markers(markers)
            return self._create_acceptance(directory, confidence, classification,
                                           counts, markers, samples)
        
        # Medium-High confidence: Multiple code files
        if counts['accepted_code'] >= self.min_code_files:
            confidence = 70 + min(counts['accepted_code'] * 2, 30)
            classification = "Software Project"
            return self._create_acceptance(directory, confidence, classification,
                                           counts, markers, samples)
        
        # Medium confidence: Programming percentage threshold
        if programming_percentage >= self.min_programming_percentage:
            confidence = int(programming_percentage)
            classification = "Software Project"
            return self._create_acceptance(directory, confidence, classification,
                                           counts, markers, samples)
        
        # Low confidence or reject
        if accepted_files > 0:
            confidence = 30
            classification = "Possible Project"
            return self._create_acceptance(directory, confidence, classification,
                                           counts, markers, samples)
        
        # Default: REJECT
        return self._create_rejection(
            "REJECTED: Insufficient programming files detected",
            counts, markers, samples, confidence=0
        )
    
    def _create_acceptance(self, directory: str, confidence: int, classification: str,
                           counts: Dict, markers: List, samples: Dict) -> Dict:
        """Create acceptance result."""
        return {
            'valid': True,
            'confidence': confidence,
            'directory': directory,
            'classification': classification,
            'statistics': {
                'total_files': counts['total_files'],
                'accepted_code': counts['accepted_code'],
                'accepted_config': counts['accepted_config'],
                'rejected_media': counts['rejected_media'],
                'unknown_files': counts['unknown_files'],
                'programming_percentage': round((counts['accepted_code'] + counts['accepted_config']) / counts['total_files'] * 100, 2) if counts['total_files'] > 0 else 0
            },
            'project_markers': markers,
            'samples': samples,
            'rejection_reason': None
        }
    
    def _create_rejection(self, reason: str, counts: Dict, markers: List,
                          samples: Dict, confidence: int = 0) -> Dict:
        """Create rejection result."""
        total_files = counts['total_files']
        accepted_files = counts['accepted_code'] + counts['accepted_config']
        programming_percentage = (accepted_files / total_files * 100) if total_files > 0 else 0
        
        return {
            'valid': False,
            'confidence': confidence,
            'directory': None,
            'classification': 'Rejected',
            'statistics': {
                'total_files': total_files,
                'accepted_code': counts['accepted_code'],
                'accepted_config': counts['accepted_config'],
                'rejected_media': counts['rejected_media'],
                'unknown_files': counts['unknown_files'],
                'programming_percentage': round(programming_percentage, 2)
            },
            'project_markers': markers,
            'samples': samples,
            'rejection_reason': reason
        }
    
    def _classify_by_markers(self, markers: List) -> str:
        """Classify project type by markers found."""
        markers_str = ' '.join(markers)
        
        if any(m.endswith('.csproj') for m in markers) or '.sln' in markers_str:
            return 'C# Project'
        if 'package.json' in markers:
            return 'JavaScript/Node Project'
        if 'requirements.txt' in markers or 'setup.py' in markers:
            return 'Python Project'
        if 'cargo.toml' in markers:
            return 'Rust Project'
        if 'go.mod' in markers:
            return 'Go Project'
        if 'pom.xml' in markers or 'build.gradle' in markers:
            return 'Java Project'
        if 'cmakelists.txt' in markers or 'makefile' in markers:
            return 'C/C++ Project'
        
        return 'Software Project'
    
    def _create_error_result(self, error_message: str) -> Dict:
        """Creates an error result."""
        return {
            'valid': False,
            'confidence': 0,
            'error': error_message,
            'directory': None,
            'statistics': None,
            'project_markers': [],
            'samples': {},
            'rejection_reason': error_message,
            'classification': 'Error'
        }
    
    def validate_project_path(self, path: str) -> Tuple[bool, str]:
        """
        Simple validation check - returns (is_valid, message).
        """
        result = self.scan_directory(path)
        
        if result['valid']:
            return True, f"Valid project ({result['classification']})"
        else:
            return False, result.get('rejection_reason', 'Invalid project')

def main():
    """
    Main entry point for CLI usage.
    Accepts path as argument and returns JSON result.
    """
    if len(sys.argv) < 2:
        print(json.dumps({
            'error': 'Usage: python Tfile.py <directory_path> [max_depth]',
            'valid': False
        }))
        sys.exit(1)
    
    directory_path = sys.argv[1]
    
    # Optional: max depth from argument
    max_depth = None
    if len(sys.argv) >= 3:
        try:
            max_depth = int(sys.argv[2])
        except ValueError:
            pass
    
    identifier = ProjectIdentifier()
    result = identifier.scan_directory(directory_path, max_depth)
    
    # Output as JSON for C# consumption
    print(json.dumps(result, indent=2))
    
    # Exit code: 0 for valid, 1 for invalid
    sys.exit(0 if result['valid'] else 1)

if __name__ == '__main__':
    main()
