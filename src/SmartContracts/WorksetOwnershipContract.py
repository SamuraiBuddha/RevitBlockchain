"""
Workset Ownership Smart Contract
Manages ownership and borrowing rules for Revit worksets
"""

from typing import Dict, List, Optional
from datetime import datetime, timedelta
import hashlib
import json


class WorksetOwnershipContract:
    """Smart contract for managing workset ownership in Revit projects"""
    
    def __init__(self):
        # State variables
        self.workset_owners: Dict[str, str] = {}  # workset_id -> owner_id
        self.workset_metadata: Dict[str, dict] = {}  # workset_id -> metadata
        self.borrow_requests: List[dict] = []
        self.borrow_history: List[dict] = []
        self.ownership_history: List[dict] = []
        self.active_borrows: Dict[str, List[dict]] = {}  # workset_id -> list of borrows
        
        # Contract settings
        self.borrow_timeout_hours = 24
        self.max_concurrent_borrows = 10
        
    def register_workset(self, workset_id: str, workset_name: str, 
                        owner: str, document_guid: str, **kwargs) -> dict:
        """Register a new workset or update existing one"""
        
        # Validate inputs
        if not all([workset_id, workset_name, owner]):
            return {"success": False, "error": "Missing required parameters"}
        
        # Check if workset exists
        is_new = workset_id not in self.workset_owners
        
        # Update ownership
        self.workset_owners[workset_id] = owner
        
        # Update metadata
        self.workset_metadata[workset_id] = {
            "name": workset_name,
            "document_guid": document_guid,
            "registered_at": datetime.utcnow().isoformat(),
            "is_editable": kwargs.get("is_editable", True),
            "last_modified": datetime.utcnow().isoformat()
        }
        
        # Log to history
        self.ownership_history.append({
            "workset_id": workset_id,
            "action": "registered" if is_new else "updated",
            "owner": owner,
            "timestamp": datetime.utcnow().isoformat(),
            "metadata": self.workset_metadata[workset_id]
        })
        
        return {
            "success": True,
            "workset_id": workset_id,
            "action": "registered" if is_new else "updated"
        }
    
    def transfer_ownership(self, workset_id: str, from_user: str, 
                          to_user: str, timestamp: int, **kwargs) -> dict:
        """Transfer ownership of a workset between users"""
        
        # Validate workset exists
        if workset_id not in self.workset_owners:
            return {"success": False, "error": "Workset not found"}
        
        # Validate current owner
        if self.workset_owners[workset_id] != from_user:
            return {
                "success": False, 
                "error": f"User {from_user} is not the current owner"
            }
        
        # Check for active borrows
        if workset_id in self.active_borrows and self.active_borrows[workset_id]:
            return {
                "success": False,
                "error": "Cannot transfer ownership with active borrows"
            }
        
        # Transfer ownership
        self.workset_owners[workset_id] = to_user
        
        # Update metadata
        self.workset_metadata[workset_id]["last_modified"] = datetime.utcnow().isoformat()
        
        # Log transfer
        self.ownership_history.append({
            "workset_id": workset_id,
            "action": "ownership_transfer",
            "from_user": from_user,
            "to_user": to_user,
            "timestamp": datetime.utcfromtimestamp(timestamp / 1000000).isoformat(),
            "document_guid": kwargs.get("document_guid")
        })
        
        return {
            "success": True,
            "workset_id": workset_id,
            "new_owner": to_user,
            "transfer_id": self._generate_transfer_id(workset_id, timestamp)
        }
    
    def request_borrow(self, workset_id: str, element_ids: List[str], 
                      user_id: str, reason: str = "") -> dict:
        """Request to borrow specific elements from a workset"""
        
        # Validate workset
        if workset_id not in self.workset_owners:
            return {"success": False, "error": "Workset not found"}
        
        # Check if user is not the owner
        if self.workset_owners[workset_id] == user_id:
            return {"success": False, "error": "Owner cannot borrow from own workset"}
        
        # Check concurrent borrow limit
        user_active_borrows = sum(
            1 for borrows in self.active_borrows.values() 
            for b in borrows if b["borrower"] == user_id
        )
        if user_active_borrows >= self.max_concurrent_borrows:
            return {
                "success": False,
                "error": f"User has reached maximum concurrent borrows ({self.max_concurrent_borrows})"
            }
        
        # Create borrow request
        request_id = self._generate_request_id(workset_id, user_id)
        borrow_request = {
            "request_id": request_id,
            "workset_id": workset_id,
            "element_ids": element_ids,
            "borrower": user_id,
            "owner": self.workset_owners[workset_id],
            "reason": reason,
            "requested_at": datetime.utcnow().isoformat(),
            "status": "pending"
        }
        
        self.borrow_requests.append(borrow_request)
        
        # Auto-approve if owner has enabled it (future feature)
        # For now, return pending status
        
        return {
            "success": True,
            "request_id": request_id,
            "status": "pending",
            "owner": self.workset_owners[workset_id]
        }
    
    def approve_borrow(self, request_id: str, owner_id: str) -> dict:
        """Owner approves a borrow request"""
        
        # Find request
        request = next((r for r in self.borrow_requests 
                       if r["request_id"] == request_id), None)
        
        if not request:
            return {"success": False, "error": "Request not found"}
        
        # Validate owner
        if request["owner"] != owner_id:
            return {"success": False, "error": "Only workset owner can approve"}
        
        # Check if already processed
        if request["status"] != "pending":
            return {"success": False, "error": f"Request already {request['status']}"}
        
        # Approve request
        request["status"] = "approved"
        request["approved_at"] = datetime.utcnow().isoformat()
        request["expires_at"] = (
            datetime.utcnow() + timedelta(hours=self.borrow_timeout_hours)
        ).isoformat()
        
        # Add to active borrows
        if request["workset_id"] not in self.active_borrows:
            self.active_borrows[request["workset_id"]] = []
        
        self.active_borrows[request["workset_id"]].append({
            "borrower": request["borrower"],
            "element_ids": request["element_ids"],
            "borrowed_at": request["approved_at"],
            "expires_at": request["expires_at"],
            "request_id": request_id
        })
        
        # Add to history
        self.borrow_history.append(request.copy())
        
        return {
            "success": True,
            "request_id": request_id,
            "borrower": request["borrower"],
            "expires_at": request["expires_at"]
        }
    
    def release_borrowed(self, workset_id: str, element_ids: List[str], 
                        user_id: str) -> dict:
        """Release borrowed elements back to workset"""
        
        if workset_id not in self.active_borrows:
            return {"success": False, "error": "No active borrows for workset"}
        
        # Find user's borrows
        user_borrows = [
            b for b in self.active_borrows[workset_id] 
            if b["borrower"] == user_id
        ]
        
        if not user_borrows:
            return {"success": False, "error": "User has no active borrows"}
        
        # Release specified elements
        released = []
        for borrow in user_borrows:
            # Remove released elements
            remaining = [e for e in borrow["element_ids"] if e not in element_ids]
            
            if len(remaining) < len(borrow["element_ids"]):
                released.extend([
                    e for e in borrow["element_ids"] if e in element_ids
                ])
                
                if remaining:
                    borrow["element_ids"] = remaining
                else:
                    # All elements released, remove borrow
                    self.active_borrows[workset_id].remove(borrow)
        
        # Clean up empty workset entry
        if not self.active_borrows[workset_id]:
            del self.active_borrows[workset_id]
        
        return {
            "success": True,
            "released_elements": released,
            "released_at": datetime.utcnow().isoformat()
        }
    
    def check_expired_borrows(self) -> List[dict]:
        """Check and clean up expired borrows"""
        
        expired = []
        current_time = datetime.utcnow()
        
        for workset_id, borrows in list(self.active_borrows.items()):
            for borrow in borrows[:]:
                expires_at = datetime.fromisoformat(borrow["expires_at"])
                
                if current_time > expires_at:
                    expired.append({
                        "workset_id": workset_id,
                        "borrower": borrow["borrower"],
                        "element_ids": borrow["element_ids"],
                        "expired_at": expires_at.isoformat()
                    })
                    
                    # Remove expired borrow
                    borrows.remove(borrow)
            
            # Clean up empty entries
            if not borrows:
                del self.active_borrows[workset_id]
        
        return expired
    
    def get_workset_status(self, workset_id: str) -> dict:
        """Get current status of a workset"""
        
        if workset_id not in self.workset_owners:
            return {"success": False, "error": "Workset not found"}
        
        active_borrows = self.active_borrows.get(workset_id, [])
        
        return {
            "success": True,
            "workset_id": workset_id,
            "owner": self.workset_owners[workset_id],
            "metadata": self.workset_metadata[workset_id],
            "active_borrows": len(active_borrows),
            "borrowed_elements": sum(len(b["element_ids"]) for b in active_borrows),
            "borrowers": list(set(b["borrower"] for b in active_borrows))
        }
    
    def _generate_transfer_id(self, workset_id: str, timestamp: int) -> str:
        """Generate unique transfer ID"""
        data = f"{workset_id}-{timestamp}-transfer"
        return hashlib.sha256(data.encode()).hexdigest()[:16]
    
    def _generate_request_id(self, workset_id: str, user_id: str) -> str:
        """Generate unique request ID"""
        timestamp = datetime.utcnow().isoformat()
        data = f"{workset_id}-{user_id}-{timestamp}"
        return hashlib.sha256(data.encode()).hexdigest()[:16]


# Contract instance (would be managed by blockchain in production)
contract = WorksetOwnershipContract()
