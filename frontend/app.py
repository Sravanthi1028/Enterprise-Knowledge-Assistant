
import streamlit as st
import requests

API_URL = "http://localhost:5265/api"

st.set_page_config(
    page_title="Enterprise Knowledge Assistant",
    page_icon="📚",
    layout="wide"
)

# =====================================================
# SESSION STATE
# =====================================================

defaults = {
    "token": None,
    "user": None,
    "role": None,
    "conversation_id": None,
    "messages": []
}

for key, value in defaults.items():
    if key not in st.session_state:
        st.session_state[key] = value


def api_headers():
    return {
        "Authorization": f"Bearer {st.session_state.token}"
    }


def show_api_error(response, action):
    if response.status_code == 401:
        st.error("Session expired. Please log out and log in again.")
    elif response.status_code == 403:
        st.error("Access denied. Admin authorization is required.")
    else:
        st.error(f"{action} failed: {response.text}")


# =====================================================
# APPLICATION TITLE
# =====================================================

st.title("📚 Enterprise Knowledge Assistant")
st.caption("Your AI-powered document knowledge platform")


# =====================================================
# LOGIN
# =====================================================

if not st.session_state.token:

    st.subheader("🔐 Login")

    email = st.text_input("Email")
    password = st.text_input("Password", type="password")

    if st.button("Login"):

        try:
            response = requests.post(
                f"{API_URL}/Auth/login",
                json={
                    "email": email,
                    "password": password
                },
                timeout=30
            )

            if response.status_code == 200:

                login_data = response.json()

                st.session_state.token = login_data["token"]
                st.session_state.user = login_data.get("user", {})
                st.session_state.role = (
                    st.session_state.user.get("role", "Employee")
                )

                st.success("Login successful!")
                st.rerun()

            else:
                st.error(f"Login failed: {response.text}")

        except requests.exceptions.RequestException as e:
            st.error(f"Connection error: {e}")


# =====================================================
# MAIN APPLICATION
# =====================================================

else:

    headers = api_headers()

    # =================================================
    # SIDEBAR
    # =================================================

    with st.sidebar:

        st.success("Logged in")

        user = st.session_state.user or {}

        st.write(
            f"**Name:** {user.get('fullName', 'User')}"
        )

        st.write(
            f"**Role:** {st.session_state.role}"
        )

        if st.button("Logout"):

            st.session_state.token = None
            st.session_state.user = None
            st.session_state.role = None
            st.session_state.conversation_id = None
            st.session_state.messages = []

            st.rerun()

        st.divider()

        st.header("Navigation")

        pages = [
            "Dashboard",
            "Upload Documents",
            "Document Search",
            "AI Chat"
        ]

        if st.session_state.role == "Admin":
            pages.append("Admin Dashboard")

        page = st.radio(
            "Choose a section",
            pages
        )

    # =================================================
    # DASHBOARD
    # =================================================

    if page == "Dashboard":

        st.header("🏠 Dashboard")

        st.write(
            "Manage your documents and ask questions "
            "about your knowledge base."
        )

        if st.button("🔄 Refresh Dashboard"):
            st.rerun()

        try:

            document_response = requests.get(
                f"{API_URL}/Documents",
                headers=headers,
                timeout=30
            )

            conversation_response = requests.get(
                f"{API_URL}/Conversation/my-conversations",
                headers=headers,
                timeout=30
            )

            if document_response.status_code == 200:

                documents = document_response.json()

                total_documents = len(documents)

                completed_documents = sum(
                    1 for doc in documents
                    if (doc.get("processingStatus") or "").lower()
                    == "completed"
                )

                failed_documents = sum(
                    1 for doc in documents
                    if (doc.get("processingStatus") or "").lower()
                    == "failed"
                )

                pending_documents = sum(
                    1 for doc in documents
                    if (doc.get("processingStatus") or "").lower()
                    == "pending"
                )

                if conversation_response.status_code == 200:
                    conversations = conversation_response.json()
                else:
                    conversations = []
                    st.warning("Unable to load conversation statistics.")

                st.subheader("📊 Project Statistics")

                col1, col2, col3, col4 = st.columns(4)

                col1.metric("Total Documents", total_documents)
                col2.metric("Completed", completed_documents)
                col3.metric("Failed", failed_documents)
                col4.metric("Conversations", len(conversations))

                st.divider()

                st.subheader("📁 Recent Uploaded Documents")

                if documents:
                    st.dataframe(
                        documents[:5],
                        use_container_width=True,
                        hide_index=True
                    )
                else:
                    st.info("No documents uploaded yet.")

                st.divider()

                st.subheader("📈 Document Processing Summary")

                st.write(f"✅ Completed: {completed_documents}")
                st.write(f"❌ Failed: {failed_documents}")
                st.write(f"⏳ Pending: {pending_documents}")

                if total_documents > 0:

                    percentage = (
                        completed_documents / total_documents
                    ) * 100

                    st.progress(percentage / 100)

                    st.caption(
                        f"Processing completion: {percentage:.1f}%"
                    )

                st.divider()

                st.subheader("💬 Recent Conversations")

                if conversations:

                    recent_conversations = [
                        {
                            "Conversation ID": c["conversationId"],
                            "Title": c["title"],
                            "Created At": c["createdAt"]
                        }
                        for c in conversations[:5]
                    ]

                    st.dataframe(
                        recent_conversations,
                        use_container_width=True,
                        hide_index=True
                    )

                else:
                    st.info("No conversations available.")

            else:
                show_api_error(document_response, "Dashboard loading")

        except requests.exceptions.RequestException as e:
            st.error(f"Dashboard connection error: {e}")

    # =================================================
    # DOCUMENT UPLOAD
    # =================================================

    elif page == "Upload Documents":

        st.header("📤 Upload Documents")

        title = st.text_input("Document Title")

        uploaded_file = st.file_uploader(
            "Choose PDF, DOCX or TXT",
            type=["pdf", "docx", "txt"]
        )

        if st.button("Upload Document"):

            if not title.strip():
                st.warning("Please enter a document title.")

            elif uploaded_file is None:
                st.warning("Please select a document.")

            else:

                files = {
                    "file": (
                        uploaded_file.name,
                        uploaded_file.getvalue(),
                        uploaded_file.type
                    )
                }

                try:

                    response = requests.post(
                        f"{API_URL}/Documents/upload",
                        headers=headers,
                        files=files,
                        data={"title": title},
                        timeout=300
                    )

                    if response.status_code == 200:

                        result = response.json()
                        document = result["document"]

                        st.success(result["message"])

                        st.subheader("Upload Details")

                        st.write("Document ID:", document["documentId"])
                        st.write("Title:", document["title"])
                        st.write("File Name:", document["fileName"])

                        st.write(
                            "Processing Status:",
                            document["processingStatus"]
                        )

                        st.write(
                            "Total Chunks:",
                            document["totalChunks"]
                        )

                        st.write(
                            "Embeddings Generated:",
                            document["embeddingsGenerated"]
                        )

                    else:
                        show_api_error(response, "Upload")

                except requests.exceptions.RequestException as e:
                    st.error(f"Upload error: {e}")

        st.divider()

        st.subheader("📁 Uploaded Documents")

        if st.button("Refresh Documents"):

            try:

                response = requests.get(
                    f"{API_URL}/Documents",
                    headers=headers,
                    timeout=30
                )

                if response.status_code == 200:

                    documents = response.json()

                    if documents:
                        st.dataframe(
                            documents,
                            use_container_width=True
                        )
                    else:
                        st.info("No documents found.")

                else:
                    show_api_error(response, "Document loading")

            except requests.exceptions.RequestException as e:
                st.error(f"Connection error: {e}")

    # =================================================
    # DOCUMENT SEARCH
    # =================================================

    elif page == "Document Search":

        st.header("🔍 Document Search")

        st.write(
            "Search uploaded documents using keywords, "
            "semantic meaning or hybrid retrieval."
        )

        search_query = st.text_input(
            "Enter your search query",
            placeholder="Example: What is a Turing Machine?"
        )

        search_type = st.selectbox(
            "Choose Search Method",
            [
                "Keyword Search",
                "Semantic Search",
                "Hybrid Search"
            ]
        )

        if st.button("Search Documents"):

            if not search_query.strip():

                st.warning("Please enter a search query.")

            else:

                endpoints = {
                    "Keyword Search": "/Search",
                    "Semantic Search": "/Search/semantic",
                    "Hybrid Search": "/Search/hybrid"
                }

                endpoint = endpoints[search_type]

                try:

                    with st.spinner("Searching documents..."):

                        response = requests.post(
                            f"{API_URL}{endpoint}",
                            headers=headers,
                            json={
                                "query": search_query
                            },
                            timeout=300
                        )

                    if response.status_code == 200:

                        result = response.json()

                        st.success(
                            f"Found {result['totalResults']} results!"
                        )

                        st.subheader(
                            f"📚 {result['searchType']} Results"
                        )

                        results = result.get("results", [])

                        if results:

                            for index, item in enumerate(results, 1):

                                title = item.get(
                                    "title",
                                    "Untitled Document"
                                )

                                with st.expander(
                                    f"Result {index}: {title}"
                                ):

                                    st.write(
                                        "Document ID:",
                                        item.get("documentId", "N/A")
                                    )

                                    st.write(
                                        "Document Title:",
                                        title
                                    )

                                    st.write(
                                        "File Name:",
                                        item.get("fileName", "N/A")
                                    )

                                    st.write(
                                        "Chunk ID:",
                                        item.get("chunkId", "N/A")
                                    )

                                    st.markdown("**Content:**")

                                    st.write(
                                        item.get("content", "")
                                    )

                                    if "semanticScore" in item:
                                        st.write(
                                            "Semantic Score:",
                                            item["semanticScore"]
                                        )

                                    if "keywordScore" in item:
                                        st.write(
                                            "Keyword Score:",
                                            item["keywordScore"]
                                        )

                                    if "combinedScore" in item:
                                        st.write(
                                            "Combined Score:",
                                            item["combinedScore"]
                                        )

                        else:
                            st.info("No relevant documents found.")

                    else:
                        show_api_error(response, "Search")

                except requests.exceptions.RequestException as e:
                    st.error(f"Search error: {e}")

    # =================================================
    # AI CHAT
    # =================================================

    elif page == "AI Chat":

        st.header("💬 Ask Your Documents")

        st.subheader("📚 Previous Conversations")

        try:

            history_response = requests.get(
                f"{API_URL}/Conversation/my-conversations",
                headers=headers,
                timeout=30
            )

            if history_response.status_code == 200:

                conversations = history_response.json()

                if conversations:

                    conversation_options = {
                        f'{c["title"]} (ID: {c["conversationId"]})':
                        c["conversationId"]
                        for c in conversations
                    }

                    selected_conversation = st.selectbox(
                        "Select a previous conversation",
                        options=list(conversation_options.keys()),
                        key="history_selection"
                    )

                    if st.button("Load Conversation"):

                        selected_id = conversation_options[
                            selected_conversation
                        ]

                        messages_response = requests.get(
                            f"{API_URL}/Conversation/"
                            f"{selected_id}/messages",
                            headers=headers,
                            timeout=30
                        )

                        if messages_response.status_code == 200:

                            saved_messages = messages_response.json()

                            st.session_state.conversation_id = selected_id

                            st.session_state.messages = [
                                {
                                    "role": (
                                        "assistant"
                                        if message["role"].lower()
                                        == "assistant"
                                        else "user"
                                    ),
                                    "content": message["content"],
                                    "sources": []
                                }
                                for message in saved_messages
                            ]

                            st.success(
                                "Conversation loaded successfully!"
                            )

                            st.rerun()

                        else:
                            show_api_error(
                                messages_response,
                                "Conversation loading"
                            )

                else:
                    st.info("No previous conversations found.")

            else:
                show_api_error(history_response, "History loading")

        except requests.exceptions.RequestException as e:
            st.error(f"History error: {e}")

        # =============================================
        # DELETE A PREVIOUS CONVERSATION
        # =============================================

        if (
            "history_response" in locals()
            and history_response.status_code == 200
            and conversations
        ):

            st.divider()
            st.subheader("🗑️ Delete a Conversation")

            delete_options = {
                f'{c["title"]} (ID: {c["conversationId"]})':
                c["conversationId"]
                for c in conversations
            }

            delete_label = st.selectbox(
                "Select conversation to delete",
                options=list(delete_options.keys()),
                key="delete_conversation_selection"
            )

            delete_conversation_id = delete_options[delete_label]

            confirm_conversation_delete = st.checkbox(
                f"I understand that conversation ID "
                f"{delete_conversation_id} and its messages "
                "will be permanently deleted.",
                key=f"confirm_conversation_delete_{delete_conversation_id}"
            )

            if st.button(
                "🗑️ Delete Selected Conversation",
                disabled=not confirm_conversation_delete,
                key="delete_selected_conversation"
            ):

                try:

                    delete_response = requests.delete(
                        f"{API_URL}/Conversation/{delete_conversation_id}",
                        headers=headers,
                        timeout=30
                    )

                    if delete_response.status_code == 200:

                        if (
                            st.session_state.conversation_id
                            == delete_conversation_id
                        ):
                            st.session_state.conversation_id = None
                            st.session_state.messages = []

                        st.session_state.pop("history_selection", None)
                        st.session_state.pop(
                            "delete_conversation_selection",
                            None
                        )

                        st.session_state.pop(
                            f"confirm_conversation_delete_{delete_conversation_id}",
                            None
                        )

                        st.success(
                            "Conversation and its messages deleted successfully."
                        )

                        st.rerun()

                    else:
                        show_api_error(
                            delete_response,
                            "Conversation deletion"
                        )

                except requests.exceptions.RequestException as e:
                    st.error(f"Conversation deletion error: {e}")

        st.divider()

        if st.button("➕ Start New Conversation"):

            st.session_state.conversation_id = None
            st.session_state.messages = []

            st.rerun()

        if st.session_state.conversation_id is None:

            conversation_title = st.text_input(
                "Conversation Title",
                value="New Conversation"
            )

            if st.button("Create Conversation"):

                if not conversation_title.strip():

                    st.warning("Enter a conversation title.")

                else:

                    try:

                        response = requests.post(
                            f"{API_URL}/Conversation/create",
                            headers=headers,
                            json={
                                "title": conversation_title
                            },
                            timeout=30
                        )

                        if response.status_code == 200:

                            data = response.json()

                            st.session_state.conversation_id = (
                                data["conversationId"]
                            )

                            st.session_state.messages = []

                            st.success("Conversation created!")

                            st.rerun()

                        else:
                            show_api_error(response, "Conversation creation")

                    except requests.exceptions.RequestException as e:
                        st.error(f"Conversation error: {e}")

        else:

            st.info(
                f"Active Conversation ID: "
                f"{st.session_state.conversation_id}"
            )

            # DISPLAY CHAT AND CITATIONS

            for message in st.session_state.messages:

                with st.chat_message(message["role"]):

                    st.markdown(message["content"])

                    sources = message.get("sources", [])

                    if message["role"] == "assistant" and sources:

                        with st.expander("📚 View Source Citations"):

                            for index, source in enumerate(sources, 1):

                                st.markdown(f"**Source {index}**")

                                st.write(
                                    "Document:",
                                    source.get("title", "Unknown")
                                )

                                st.write(
                                    "File:",
                                    source.get("fileName", "Unknown")
                                )

                                st.write(
                                    "Chunk ID:",
                                    source.get("chunkId", "Unknown")
                                )

                                st.write(
                                    "Content:",
                                    source.get("content", "")
                                )

                                st.divider()

            # ASK QUESTION

            question = st.chat_input(
                "Ask a question about your uploaded documents..."
            )

            if question:

                st.session_state.messages.append({
                    "role": "user",
                    "content": question,
                    "sources": []
                })

                try:

                    response = requests.post(
                        f"{API_URL}/RAG/ask",
                        headers=headers,
                        json={
                            "query": question,
                            "conversationId": (
                                st.session_state.conversation_id
                            )
                        },
                        timeout=300
                    )

                    if response.status_code == 200:

                        result = response.json()

                        answer = result["answer"]

                        sources = result.get("sources", [])

                        st.session_state.messages.append({
                            "role": "assistant",
                            "content": answer,
                            "sources": sources
                        })

                        st.rerun()

                    else:

                        st.session_state.messages.pop()

                        show_api_error(response, "Question")

                except requests.exceptions.RequestException as e:

                    st.session_state.messages.pop()

                    st.error(f"Chat error: {e}")

    # =================================================
    # ADMIN DASHBOARD
    # =================================================

    elif page == "Admin Dashboard":

        st.header("🛡️ Admin Dashboard")

        st.caption(
            "Manage registered users and uploaded documents."
        )

        if st.session_state.role != "Admin":

            st.error(
                "Access denied. Admin role is required."
            )

        else:

            users_tab, documents_tab = st.tabs(
                [
                    "👥 User Management",
                    "📁 Document Management"
                ]
            )

            # =============================================
            # USER MANAGEMENT
            # =============================================

            with users_tab:

                st.subheader("Registered Users")

                if st.button(
                    "🔄 Refresh Users",
                    key="refresh_admin_users"
                ):
                    st.rerun()

                try:

                    users_response = requests.get(
                        f"{API_URL}/Auth/admin/users",
                        headers=headers,
                        timeout=30
                    )

                    if users_response.status_code == 200:

                        users_data = users_response.json()

                        users = users_data.get("users", [])

                        admins_count = sum(
                            1 for user in users
                            if user.get("role") == "Admin"
                        )

                        employees_count = sum(
                            1 for user in users
                            if user.get("role") == "Employee"
                        )

                        col1, col2, col3 = st.columns(3)

                        col1.metric(
                            "Total Users",
                            users_data.get("totalUsers", len(users))
                        )

                        col2.metric(
                            "Admins",
                            admins_count
                        )

                        col3.metric(
                            "Employees",
                            employees_count
                        )

                        st.divider()

                        if users:

                            user_rows = [
                                {
                                    "User ID": user.get("userId"),
                                    "Full Name": user.get("fullName"),
                                    "Email": user.get("email"),
                                    "Role": user.get("role"),
                                    "Created At": user.get("createdAt")
                                }
                                for user in users
                            ]

                            st.dataframe(
                                user_rows,
                                use_container_width=True,
                                hide_index=True
                            )

                            st.divider()

                            st.subheader("Update User Role")

                            current_user_id = (
                                st.session_state.user or {}
                            ).get("userId")

                            editable_users = [
                                user for user in users
                                if user.get("userId") != current_user_id
                            ]

                            if editable_users:

                                user_options = {
                                    (
                                        f"{user.get('fullName')} "
                                        f"({user.get('email')}) "
                                        f"— {user.get('role')}"
                                    ): user
                                    for user in editable_users
                                }

                                selected_user_label = st.selectbox(
                                    "Select a user",
                                    list(user_options.keys()),
                                    key="admin_selected_user"
                                )

                                selected_user = user_options[
                                    selected_user_label
                                ]

                                current_role = selected_user.get(
                                    "role",
                                    "Employee"
                                )

                                new_role = st.selectbox(
                                    "New role",
                                    ["Employee", "Admin"],
                                    index=(
                                        0 if current_role == "Employee"
                                        else 1
                                    ),
                                    key="admin_new_role"
                                )

                                if st.button(
                                    "Update Role",
                                    key="update_user_role"
                                ):

                                    if new_role == current_role:

                                        st.info(
                                            "The selected user already has this role."
                                        )

                                    else:

                                        try:

                                            role_response = requests.put(
                                                f"{API_URL}/Auth/admin/users/"
                                                f"{selected_user['userId']}/role",
                                                headers=headers,
                                                json=new_role,
                                                timeout=30
                                            )

                                            if role_response.status_code == 200:

                                                st.success(
                                                    role_response.json().get(
                                                        "message",
                                                        "User role updated successfully."
                                                    )
                                                )

                                                st.rerun()

                                            else:

                                                show_api_error(
                                                    role_response,
                                                    "Role update"
                                                )

                                        except requests.exceptions.RequestException as e:

                                            st.error(
                                                f"Role update error: {e}"
                                            )

                            else:

                                st.info(
                                    "No other users are available for role management."
                                )

                        else:

                            st.info("No registered users found.")

                    else:

                        show_api_error(
                            users_response,
                            "User management"
                        )

                except requests.exceptions.RequestException as e:

                    st.error(
                        f"User management connection error: {e}"
                    )

            # =============================================
            # DOCUMENT MANAGEMENT
            # =============================================

            with documents_tab:

                st.subheader("All Uploaded Documents")

                if st.button(
                    "🔄 Refresh Admin Documents",
                    key="refresh_admin_documents"
                ):
                    st.rerun()

                try:

                    admin_docs_response = requests.get(
                        f"{API_URL}/Documents/admin/all",
                        headers=headers,
                        timeout=30
                    )

                    if admin_docs_response.status_code == 200:

                        admin_docs_data = admin_docs_response.json()

                        admin_documents = admin_docs_data.get(
                            "documents",
                            []
                        )

                        total_admin_documents = admin_docs_data.get(
                            "totalDocuments",
                            len(admin_documents)
                        )

                        st.metric(
                            "Total Documents",
                            total_admin_documents
                        )

                        if admin_documents:

                            document_rows = [
                                {
                                    "Document ID": doc.get("documentId"),
                                    "Title": doc.get("title"),
                                    "File Name": doc.get("fileName"),
                                    "Status": doc.get("processingStatus"),
                                    "Uploaded By User ID": doc.get(
                                        "uploadedByUserId"
                                    ),
                                    "Uploaded At": doc.get("uploadedAt"),
                                    "File Size (bytes)": doc.get("fileSize")
                                }
                                for doc in admin_documents
                            ]

                            st.dataframe(
                                document_rows,
                                use_container_width=True,
                                hide_index=True
                            )

                            st.divider()

                            st.subheader("Delete a Document")

                            document_options = {
                                (
                                    f"ID {doc.get('documentId')} — "
                                    f"{doc.get('title')} "
                                    f"({doc.get('fileName')})"
                                ): doc
                                for doc in admin_documents
                            }

                            selected_document_label = st.selectbox(
                                "Select the document to delete",
                                list(document_options.keys()),
                                key="admin_selected_document"
                            )

                            selected_document = document_options[
                                selected_document_label
                            ]

                            confirm_delete = st.checkbox(
                                (
                                    f"I understand that document ID "
                                    f"{selected_document.get('documentId')} "
                                    "and its chunks and embeddings will "
                                    "be permanently deleted."
                                ),
                                key="confirm_admin_document_delete"
                            )

                            if st.button(
                                "🗑️ Delete Selected Document",
                                type="primary",
                                disabled=not confirm_delete,
                                key="delete_admin_document"
                            ):

                                try:

                                    delete_response = requests.delete(
                                        f"{API_URL}/Documents/admin/"
                                        f"{selected_document['documentId']}",
                                        headers=headers,
                                        timeout=30
                                    )

                                    if delete_response.status_code == 200:

                                        st.success(
                                            delete_response.json().get(
                                                "message",
                                                "Document deleted successfully."
                                            )
                                        )

                                        st.rerun()

                                    else:

                                        show_api_error(
                                            delete_response,
                                            "Document deletion"
                                        )

                                except requests.exceptions.RequestException as e:

                                    st.error(
                                        f"Document deletion error: {e}"
                                    )

                        else:

                            st.info(
                                "No documents are available to manage."
                            )

                    else:

                        show_api_error(
                            admin_docs_response,
                            "Admin document loading"
                        )

                except requests.exceptions.RequestException as e:

                    st.error(
                        f"Document management connection error: {e}"
                    )


# =====================================================
# APPLICATION FOOTER
# =====================================================

st.divider()

st.markdown(
    "<div style='text-align:center; color:gray;'>"
    "Enterprise Knowledge Assistant | AI-Powered Document Intelligence"
    "</div>",
    unsafe_allow_html=True
)
