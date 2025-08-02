const API_BASE_URL = 'http://localhost:5001';
        
let selectedFiles = [];
let uploadedFilePaths = [];
let isProcessed = false;
let questionCount = 0;

// DOM elements
const uploadArea = document.getElementById('uploadArea');
const fileInput = document.getElementById('fileInput');
const fileList = document.getElementById('fileList');
const processBtn = document.getElementById('processBtn');
const questionInput = document.getElementById('questionInput');
const askBtn = document.getElementById('askBtn');
const answerContent = document.getElementById('answerContent');
const sourcesSection = document.getElementById('sourcesSection');
const sourcesList = document.getElementById('sourcesList');
const uploadLoading = document.getElementById('uploadLoading');
const questionLoading = document.getElementById('questionLoading');
const alertsContainer = document.getElementById('alerts');
const statusBadge = document.getElementById('statusBadge');
const statusText = document.getElementById('statusText');
const charCount = document.getElementById('charCount');

// Initialize
document.addEventListener('DOMContentLoaded', function() {
    setupEventListeners();
    updateStatus('ready');
});

function updateStatus(status) {
    const statusMap = {
        'ready': { text: 'Ready', active: false },
        'processing': { text: 'Processing', active: true },
        'active': { text: 'Active', active: true },
        'error': { text: 'Error', active: false }
    };
    
    const statusInfo = statusMap[status] || statusMap['ready'];
    statusText.textContent = statusInfo.text;
    
    if (statusInfo.active) {
        statusBadge.classList.add('active');
    } else {
        statusBadge.classList.remove('active');
    }
}

function updateStats() {
    document.getElementById('docCount').textContent = selectedFiles.length;
    document.getElementById('questionCount').textContent = questionCount;
}

function setupEventListeners() {
    // File input
    fileInput.addEventListener('change', handleFileSelect);
    
    // Upload area events
    uploadArea.addEventListener('click', () => {
        if (!isProcessed) {
            fileInput.click();
        }
    });
    uploadArea.addEventListener('dragover', handleDragOver);
    uploadArea.addEventListener('dragleave', handleDragLeave);
    uploadArea.addEventListener('drop', handleDrop);
    
    // Question input
    questionInput.addEventListener('input', function() {
        const length = this.value.length;
        charCount.textContent = `${length} / 500`;
        askBtn.disabled = !this.value.trim() || !isProcessed;
    });
    
    // Button click events
    processBtn.addEventListener('click', processPdfs);
    document.getElementById('clearBtn').addEventListener('click', clearFiles);
    document.getElementById('testBtn').addEventListener('click', testConnection);
    askBtn.addEventListener('click', askQuestion);
}

function handleFileSelect(event) {
    const files = Array.from(event.target.files);
    addFiles(files);
}

function handleDragOver(event) {
    if (isProcessed) return;
    event.preventDefault();
    uploadArea.classList.add('dragover');
}

function handleDragLeave(event) {
    event.preventDefault();
    uploadArea.classList.remove('dragover');
}

function handleDrop(event) {
    if (isProcessed) return;
    event.preventDefault();
    uploadArea.classList.remove('dragover');
    
    const files = Array.from(event.dataTransfer.files);
    const pdfFiles = files.filter(file => file.type === 'application/pdf');
    
    if (pdfFiles.length !== files.length) {
        showAlert('Only PDF files are allowed', 'error');
    }
    
    addFiles(pdfFiles);
}

function addFiles(files) {  
    // Prevent adding files if already processed
    if (isProcessed) {
        showAlert('Cannot add files after processing. Use Clear All to reset.', 'error');
        return;
    }
    
    files.forEach(file => {
        if (file.type === 'application/pdf') {
            selectedFiles.push(file);
        } else {
            showAlert(`${file.name} is not a PDF file`, 'error');
        }
    });
    updateFileList();
    updateStats();
    processBtn.disabled = selectedFiles.length === 0;
}

// Enhanced updateFileList function with better remove button handling
function updateFileList() {
    fileList.innerHTML = '';
    
    selectedFiles.forEach((file, index) => {
        const fileItem = document.createElement('div');
        fileItem.className = 'file-item';
        
        fileItem.innerHTML = `
            <div class="file-info">
                <div class="file-icon">
                    <svg class="icon" viewBox="0 0 20 20" fill="currentColor">
                        <path fill-rule="evenodd" d="M4 4a2 2 0 00-2 2v8a2 2 0 002 2h12a2 2 0 002-2V6a2 2 0 00-2-2H4zm3 5a1 1 0 011-1h4a1 1 0 110 2H8a1 1 0 01-1-1zm0 3a1 1 0 011-1h4a1 1 0 110 2H8a1 1 0 01-1-1z" clip-rule="evenodd" />
                    </svg>
                </div>
                <div class="file-details">
                    <div class="file-name">${file.name}</div>
                    <div class="file-size">${formatFileSize(file.size)}</div>
                </div>
            </div>
            <button class="remove-btn ${isProcessed ? 'disabled' : ''}" 
                    ${isProcessed ? 'disabled' : ''} 
                    data-file-index="${index}"
                    title="${isProcessed ? 'Cannot remove files after processing' : 'Remove file'}">
                <svg class="icon" viewBox="0 0 20 20" fill="currentColor" style="opacity: ${isProcessed ? '0.3' : '1'};">
                    <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
                </svg>
            </button>
        `;
        
        fileList.appendChild(fileItem);
    });
    
    // Add event listeners to remove buttons after they're created
    attachRemoveButtonListeners();
}

// Separate function to handle remove button event listeners
function attachRemoveButtonListeners() {
    const removeButtons = document.querySelectorAll('.remove-btn');
    
    removeButtons.forEach(button => {
        // Remove any existing listeners by cloning the button
        const newButton = button.cloneNode(true);
        button.parentNode.replaceChild(newButton, button);
    });
    
    // Re-select buttons after cloning (to remove old listeners)
    const freshRemoveButtons = document.querySelectorAll('.remove-btn');
    
    freshRemoveButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();
            
            if (isProcessed) {
                showAlert('Cannot remove files after processing. Use Clear All to reset.', 'error');
                return false;
            }
            
            const fileIndex = parseInt(this.getAttribute('data-file-index'));
            removeFile(fileIndex);
        });
    });
}

// Updated removeFile function
function removeFile(index) {
    // Prevent removal if files are processed
    if (isProcessed) {
        showAlert('Cannot remove files after processing. Use Clear All to reset.', 'error');
        return false;
    }
    
    if (index >= 0 && index < selectedFiles.length) {
        const removedFile = selectedFiles[index];
        selectedFiles.splice(index, 1);
        updateFileList();
        updateStats();
        processBtn.disabled = selectedFiles.length === 0;
    }
}

// Enhanced clearFiles function
async function clearFiles() {
    try {
        // Call the comprehensive cleanup endpoint
        const response = await fetch(`${API_BASE_URL}/cleanup`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ 
                ClearAll: true,           // Delete all files from disk
                ForceNewSession: true     // Reset everything
            })
        });
        
        if (!response.ok) {
            throw new Error('Cleanup failed');
        }
        
        // Reset frontend state
        selectedFiles = [];
        uploadedFilePaths = [];
        isProcessed = false;
        
        updateFileList();
        updateStats();
        
        // Re-enable upload area
        uploadArea.style.pointerEvents = 'auto';
        uploadArea.style.opacity = '1';
        fileInput.disabled = false;
        
        questionInput.disabled = true;
        askBtn.disabled = true;
        answerContent.textContent = 'Upload and process your PDF documents to start asking questions.';
        sourcesSection.style.display = 'none';
        fileInput.value = '';
        
        document.getElementById('processBtnText').textContent = 'Process Documents';
        processBtn.classList.remove('btn-success');
        processBtn.classList.add('btn-primary');
        
        updateStatus('ready');
        showAlert('All files and data cleared successfully', 'success');
        
    } catch (error) {
        showAlert('Error during cleanup. Please refresh the page.', 'error');
    }
}

function formatFileSize(bytes) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

async function testConnection() {
    try {
        const response = await fetch(`${API_BASE_URL}/api/pdfqa/health`);
        
        if (response.ok) {
            const data = await response.json();
            showAlert('Connection successful', 'success');
        } else {
            showAlert(`Connection failed: ${response.status}`, 'error');
        }
    } catch (error) {
        showAlert(`Connection error: ${error.message}`, 'error');
    }
}

// Enhanced processPdfs function with better button disabling
async function processPdfs() {
    if (selectedFiles.length === 0) {
        showAlert('Please select PDF files first', 'error');
        return;
    }

    const uploadCard = document.querySelector('.card');
    const loadingOverlay = document.createElement('div');
    loadingOverlay.className = 'loading-overlay';
    loadingOverlay.innerHTML = `
        <div class="loading-content">
            <div class="loading-spinner"></div>
            <div class="loading-text">Processing documents...</div>
        </div>
    `;
    uploadCard.style.position = 'relative';
    uploadCard.appendChild(loadingOverlay);

    try {
        updateStatus('processing');
        processBtn.disabled = true;

        // Upload files
        const formData = new FormData();
        selectedFiles.forEach(file => {
            formData.append('files', file);
        });

        const uploadResponse = await fetch(`${API_BASE_URL}/api/pdfqa/upload`, {
            method: 'POST',
            body: formData
        });

        if (!uploadResponse.ok) {
            throw new Error(`Upload failed: ${uploadResponse.status}`);
        }

        const uploadResult = await uploadResponse.json();
        
        if (uploadResult.files && uploadResult.files.length > 0) {
            const filePaths = uploadResult.files.map(file => file.filePath);
            uploadedFilePaths = filePaths;
            
            // Process PDFs
            const processResponse = await fetch(`${API_BASE_URL}/api/pdfqa/process`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ PdfPaths: filePaths })
            });

            if (!processResponse.ok) {
                throw new Error(`Process failed: ${processResponse.status}`);
            }

            const processResult = await processResponse.json();
            
            if (processResult.documentsProcessed && processResult.documentsProcessed > 0) {
                // SET PROCESSED FLAG FIRST
                isProcessed = true;
                updateStatus('active');
                
                // Update the file list to disable remove buttons
                updateFileList();
                
                // Additional safeguard: disable all remove buttons
                disableAllRemoveButtons();
                
                // Disable upload area
                uploadArea.style.pointerEvents = 'none';
                uploadArea.style.opacity = '0.6';
                fileInput.disabled = true;
                
                // Update UI
                questionInput.disabled = false;
                questionInput.placeholder = "What would you like to know about your documents?";
                
                document.getElementById('processBtnText').textContent = 'Documents Ready';
                processBtn.classList.remove('btn-primary');
                processBtn.classList.add('btn-success');
                
                answerContent.innerHTML = ` 
                    <div style="text-align: center; color: var(--success);">
                        <svg style="width: 48px; height: 48px; margin-bottom: 1rem;" viewBox="0 0 20 20" fill="currentColor">
                            <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
                        </svg>
                        <div style="font-size: 1.125rem; font-weight: 600;">Documents processed successfully!</div>
                        <div style="color: var(--gray-600); margin-top: 0.5rem;">Ask any question about your PDFs</div>
                    </div>
                `;
                
                showAlert(`${processResult.documentsProcessed} documents ready for analysis`, 'success');
                
                // Focus question input
                setTimeout(() => questionInput.focus(), 100);
            }
        }
    } catch (error) {
        updateStatus('error');
        showAlert(`Error: ${error.message}`, 'error');
    } finally {
        processBtn.disabled = false;
        uploadCard.removeChild(loadingOverlay);
    }
}

// Additional helper function to ensure all remove buttons are disabled
function disableAllRemoveButtons() {
    const removeButtons = document.querySelectorAll('.remove-btn');
    removeButtons.forEach(button => {
        button.disabled = true;
        button.classList.add('disabled');
        button.style.opacity = '0.5';
        button.style.cursor = 'not-allowed';
        button.style.pointerEvents = 'none';
        
        // Make the SVG more visually disabled
        const svg = button.querySelector('svg');
        if (svg) {
            svg.style.opacity = '0.3';
        }
    });
}

async function askQuestion() {
    const question = questionInput.value.trim();
    
    if (!question) {
        showAlert('Please enter a question', 'error');
        return;
    }

    const qaCard = document.querySelector('.qa-section');
    const loadingOverlay = document.createElement('div');
    loadingOverlay.className = 'loading-overlay';
    loadingOverlay.innerHTML = `
        <div class="loading-content">
            <div class="loading-spinner"></div>
            <div class="loading-text">Analyzing your question...</div>
        </div>
    `;
    qaCard.appendChild(loadingOverlay);

    try {
        askBtn.disabled = true;

        const response = await fetch(`${API_BASE_URL}/api/pdfqa/ask`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ question })
        });

        if (!response.ok) {
            throw new Error(`Request failed: ${response.status}`);
        }

        const result = await response.json();
        
        if (result.answer) {
            answerContent.textContent = result.answer;
            
            // Display sources
            if (result.sources && result.sources.length > 0) {
                sourcesList.innerHTML = '';
                result.sources.forEach(source => {
                    const sourceItem = document.createElement('div');
                    sourceItem.className = 'source-item';
                    sourceItem.innerHTML = `
                        <svg class="icon" style="width: 16px; height: 16px;" viewBox="0 0 20 20" fill="currentColor">
                            <path d="M9 4.804A7.968 7.968 0 005.5 4c-1.255 0-2.443.29-3.5.804v10A7.969 7.969 0 015.5 14c1.669 0 3.218.51 4.5 1.385A7.962 7.962 0 0114.5 14c1.255 0 2.443.29 3.5.804v-10A7.968 7.968 0 0014.5 4c-1.255 0-2.443.29-3.5.804V12a1 1 0 11-2 0V4.804z" />
                        </svg>
                        ${source}
                    `;
                    sourcesList.appendChild(sourceItem);
                });
                sourcesSection.style.display = 'block';
            }
            
            questionCount++;
            updateStats();
        }
    } catch (error) {
        answerContent.textContent = 'Unable to process your question. Please try again.';
        showAlert(`Error: ${error.message}`, 'error');
    } finally {
        askBtn.disabled = false;
        qaCard.removeChild(loadingOverlay);
    }
}

function showAlert(message, type) {
    const alert = document.createElement('div');
    alert.className = `alert alert-${type}`;
    alert.innerHTML = `
        <svg class="icon" viewBox="0 0 20 20" fill="currentColor">
            ${type === 'success' ? '<path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />' :
              type === 'error' ? '<path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd" />' :
              '<path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd" />'}
        </svg>
        <span>${message}</span>
        <button class="alert-close" onclick="this.parentElement.remove()">
            <svg class="icon" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
            </svg>
        </button>
    `;
    
    alertsContainer.appendChild(alert);
    
    setTimeout(() => {
        if (alert.parentNode) {
            alert.style.opacity = '0';
            setTimeout(() => alert.remove(), 300);
        }
    }, 5000);
}

// Prevent form submission on button clicks
document.addEventListener('click', function(e) {
    if (e.target.tagName === 'BUTTON') {
        e.preventDefault();
    }
});